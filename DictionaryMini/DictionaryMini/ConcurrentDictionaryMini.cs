using System;
using System.Collections.Generic;
using System.Threading;

namespace DictionaryMini
{
    public class ConcurrentDictionaryMini<TKey, TValue>
    {
        private class Node
        {
            internal TKey m_key;   //数据的key
            internal TValue m_value;  //数据值
            internal volatile Node m_next;  //当前Node的下级节点
            internal int m_hashcode;  //key的hashcode

            //构造函数
            internal Node(TKey key, TValue value, int hashcode, Node next)
            {
                m_key = key;
                m_value = value;
                m_next = next;
                m_hashcode = hashcode;
            }
        }

        private class Tables
        {
            internal readonly Node[] m_buckets;   //上文中提到的buckets
            internal readonly object[] m_locks;   //线程锁
            internal volatile int[] m_countPerLock;  //索格锁所管理的数据数量
            internal readonly IEqualityComparer<TKey> m_comparer;  //当前key对应的type的比较器

            //构造函数
            internal Tables(Node[] buckets, object[] locks, int[] countPerlock, IEqualityComparer<TKey> comparer)
            {
                m_buckets = buckets;
                m_locks = locks;
                m_countPerLock = countPerlock;
                m_comparer = comparer;
            }
        }

        private volatile Tables m_tables;

        //时否动态扩充锁的数量
        private readonly bool m_growLockArray;

        private int m_keyRehashCount;

        //在触发调整大小操作之前，每个锁的最大元素数
        private int m_budget;

        private const int DEFAULT_CAPACITY = 31;

        private const int MAX_LOCK_NUMBER = 1024;

        //构造函数
        public ConcurrentDictionaryMini() : this(DefaultConcurrencyLevel, DEFAULT_CAPACITY, true,
            EqualityComparer<TKey>.Default)
        {
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="concurrencyLevel">并发等级,默认为CPU的线程数</param>
        /// <param name="capacity">默认容量,31,超过31后会自动扩容</param>
        /// <param name="growLockArray">时否动态扩充锁的数量</param>
        /// <param name="comparer">key的比较器</param>
        internal ConcurrentDictionaryMini(int concurrencyLevel, int capacity, bool growLockArray, IEqualityComparer<TKey> comparer)
        {
            if (concurrencyLevel < 1)
            {
                throw new Exception("concurrencyLevel 必须为正数");
            }

            if (capacity < 0)
            {
                throw new Exception("capacity 不能为负数.");
            }

            if (capacity < concurrencyLevel)
            {
                capacity = concurrencyLevel;
            }

            object[] locks = new object[concurrencyLevel];
            for (int i = 0; i < locks.Length; i++)
            {
                locks[i] = new object();
            }

            int[] countPerLock = new int[locks.Length];
            Node[] buckets = new Node[capacity];
            m_tables = new Tables(buckets, locks, countPerLock, comparer);

            m_growLockArray = growLockArray;
            m_budget = buckets.Length / locks.Length;
        }

        public bool TryAdd(TKey key, TValue value)
        {
            if (key == null) throw new ArgumentNullException("key");
            TValue dummy;
            return TryAddInternal(key, value, false, true, out dummy);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            if (key == null) throw new ArgumentNullException("key");

            // We must capture the m_buckets field in a local variable. It is set to a new table on each table resize.
            Tables tables = m_tables;
            IEqualityComparer<TKey> comparer = tables.m_comparer;
            GetBucketAndLockNo(comparer.GetHashCode(key), out var bucketNo, out _, tables.m_buckets.Length, tables.m_locks.Length);

            // We can get away w/out a lock here.
            // The Volatile.Read ensures that the load of the fields of 'n' doesn't move before the load from buckets[i].
            Node n = Volatile.Read(ref tables.m_buckets[bucketNo]);

            while (n != null)
            {
                if (comparer.Equals(n.m_key, key))
                {
                    value = n.m_value;
                    return true;
                }
                n = n.m_next;
            }

            value = default(TValue);
            return false;
        }

        public bool ContainsKey(TKey key)
        {
            if (key == null) throw new ArgumentNullException("key");

            return TryGetValue(key, out _);
        }

        public bool TryRemove(TKey key, out TValue value)
        {
            if (key == null) throw new ArgumentNullException("key");

            return TryRemoveInternal(key, out value, false, default(TValue));
        }

        public void Clear()
        {
            int locksAcquired = 0;
            try
            {
                AcquireAllLocks(ref locksAcquired);

                Tables newTables = new Tables(new Node[DEFAULT_CAPACITY], m_tables.m_locks, new int[m_tables.m_countPerLock.Length], m_tables.m_comparer);
                m_tables = newTables;
                m_budget = Math.Max(1, newTables.m_buckets.Length / newTables.m_locks.Length);
            }
            finally
            {
                ReleaseLocks(0, locksAcquired);
            }
        }

        public TValue this[TKey key]
        {
            get
            {
                TValue value;
                if (!TryGetValue(key, out value))
                {
                    throw new KeyNotFoundException();
                }

                return value;
            }
            set
            {
                if (key == null) throw new ArgumentNullException("key");
                TValue dummy;
                TryAddInternal(key, value, true, true, out dummy);
            }
        }

        #region Private

        private bool TryRemoveInternal(TKey key, out TValue value, bool matchValue, TValue oldValue)
        {
            while (true)
            {
                Tables tables = m_tables;

                IEqualityComparer<TKey> comparer = tables.m_comparer;

                int bucketNo, lockNo;

                GetBucketAndLockNo(comparer.GetHashCode(key), out bucketNo, out lockNo, tables.m_buckets.Length, tables.m_locks.Length);

                lock (tables.m_locks[lockNo])
                {
                    if (tables != m_tables)
                        continue;

                    Node prev = null;
                    for (Node curr = tables.m_buckets[bucketNo]; curr != null; curr = curr.m_next)
                    {
                        if (comparer.Equals(curr.m_key, key))
                        {
                            if (matchValue)
                            {
                                bool valuesMatch = EqualityComparer<TValue>.Default.Equals(oldValue, curr.m_value);
                                if (!valuesMatch)
                                {
                                    value = default(TValue);
                                    return false;
                                }
                            }
                            if (prev == null)
                                Volatile.Write(ref tables.m_buckets[bucketNo], curr.m_next);
                            else
                            {
                                prev.m_next = curr.m_next;
                            }

                            value = curr.m_value;
                            tables.m_countPerLock[lockNo]--;
                            return true;
                        }

                        prev = curr;
                    }
                }

                value = default(TValue);
                return false;
            }
        }

        private bool TryAddInternal(TKey key, TValue value, bool updateIfExists, bool acquireLock, out TValue resultingValue)
        {
            while (true)
            {
                int bucketNo, lockNo;
                int hashcode;

                //https://www.cnblogs.com/blurhkh/p/10357576.html
                //需要了解一下值传递和引用传递
                Tables tables = m_tables;
                IEqualityComparer<TKey> comparer = tables.m_comparer;
                hashcode = comparer.GetHashCode(key);

                GetBucketAndLockNo(hashcode, out bucketNo, out lockNo, tables.m_buckets.Length, tables.m_locks.Length);

                bool resizeDesired = false;
                bool lockTaken = false;

                try
                {
                    if (acquireLock)
                        Monitor.Enter(tables.m_locks[lockNo], ref lockTaken);

                    //如果表刚刚调整了大小，我们可能没有持有正确的锁，必须重试。
                    //当然这种情况很少见
                    if (tables != m_tables)
                        continue;

                    Node prev = null;
                    for (Node node = tables.m_buckets[bucketNo]; node != null; node = node.m_next)
                    {
                        if (comparer.Equals(node.m_key, key))
                        {
                            //key在字典里找到了。如果允许更新，则更新该key的值。
                            //我们需要为更新创建一个node，以支持不能以原子方式写入的TValue类型，因为free-lock 读取可能同时发生。
                            if (updateIfExists)
                            {
                                if (s_isValueWriteAtomic)
                                {
                                    node.m_value = value;
                                }
                                else
                                {
                                    Node newNode = new Node(node.m_key, value, hashcode, node.m_next);
                                    if (prev == null)
                                    {
                                        tables.m_buckets[bucketNo] = newNode;
                                    }
                                    else
                                    {
                                        prev.m_next = newNode;
                                    }
                                }

                                resultingValue = value;
                            }
                            else
                            {
                                resultingValue = node.m_value;
                            }

                            return false;
                        }

                        prev = node;
                    }

                    //key没有在bucket中找到,则插入该数据
                    Volatile.Write(ref tables.m_buckets[bucketNo], new Node(key, value, hashcode, tables.m_buckets[bucketNo]));
                    //当m_countPerLock超过Int Max时会抛出OverflowException
                    checked
                    {
                        tables.m_countPerLock[lockNo]++;
                    }

                    //
                    // 如果m_countPerLock[lockNo] > m_budget，则需要调整buckets的大小。
                    // GrowTable也可能会增加m_budget，但不会调整bucket table的大小。.
                    // 如果发现bucket table利用率很低，也会发生这种情况。
                    //
                    if (tables.m_countPerLock[lockNo] > m_budget)
                    {
                        resizeDesired = true;
                    }
                }
                finally
                {
                    if (lockTaken)
                        Monitor.Exit(tables.m_locks[lockNo]);
                }

                if (resizeDesired)
                {
                    GrowTable(tables, tables.m_comparer, false, m_keyRehashCount);
                }

                resultingValue = value;
                return true;
            }
        }

        private void GetBucketAndLockNo(
            int hashcode, out int bucketNo, out int lockNo, int bucketCount, int lockCount)
        {
            //0x7FFFFFFF 是long int的最大值 与它按位与数据小于等于这个最大值
            bucketNo = (hashcode & 0x7fffffff) % bucketCount;
            lockNo = bucketNo % lockCount;
        }

        // Whether TValue is a type that can be written atomically (i.e., with no danger of torn reads)
        private static readonly bool s_isValueWriteAtomic = IsValueWriteAtomic();

        private static int DefaultConcurrencyLevel
        {
            get { return Environment.ProcessorCount; }
        }

        /// <summary>
        /// Determines whether type TValue can be written atomically
        /// </summary>
        private static bool IsValueWriteAtomic()
        {
            Type valueType = typeof(TValue);

            //
            // Section 12.6.6 of ECMA CLI explains which types can be read and written atomically without
            // the risk of tearing.
            //
            // See http://www.ecma-international.org/publications/files/ECMA-ST/ECMA-335.pdf
            //
            if (valueType.IsClass)
            {
                return true;
            }
            switch (Type.GetTypeCode(valueType))
            {
                case TypeCode.Boolean:
                case TypeCode.Byte:
                case TypeCode.Char:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                    return true;

                case TypeCode.Int64:
                case TypeCode.Double:
                case TypeCode.UInt64:
                    return IntPtr.Size == 8;

                default:
                    return false;
            }
        }

        private void GrowTable(Tables tables, IEqualityComparer<TKey> newComparer, bool regenerateHashKeys,
            int rehashCount)
        {
            int locksAcquired = 0;
            try
            {
                //首先锁住第一个lock进行resize操作.
                AcquireLocks(0, 1, ref locksAcquired);

                if (regenerateHashKeys && rehashCount == m_keyRehashCount)
                {
                    tables = m_tables;
                }
                else
                {
                    if (tables != m_tables)
                        return;

                    long approxCount = 0;
                    for (int i = 0; i < tables.m_countPerLock.Length; i++)
                    {
                        approxCount += tables.m_countPerLock[i];
                    }

                    //如果bucket数组太空，则将预算加倍，而不是调整表的大小
                    if (approxCount < tables.m_buckets.Length / 4)
                    {
                        m_budget = 2 * m_budget;
                        if (m_budget < 0)
                        {
                            m_budget = int.MaxValue;
                        }

                        return;
                    }
                }

                int newLength = 0;
                bool maximizeTableSize = false;
                try
                {
                    checked
                    {
                        newLength = tables.m_buckets.Length * 2 + 1;
                        while (newLength % 3 == 0 || newLength % 5 == 0 || newLength % 7 == 0)
                        {
                            newLength += 2;
                        }
                    }
                }
                catch (OverflowException)
                {
                    maximizeTableSize = true;
                }

                if (maximizeTableSize)
                {
                    newLength = int.MaxValue;

                    m_budget = int.MaxValue;
                }

                AcquireLocks(1, tables.m_locks.Length, ref locksAcquired);

                object[] newLocks = tables.m_locks;

                //Add more locks
                if (m_growLockArray && tables.m_locks.Length < MAX_LOCK_NUMBER)
                {
                    newLocks = new object[tables.m_locks.Length * 2];
                    Array.Copy(tables.m_locks, newLocks, tables.m_locks.Length);

                    for (int i = tables.m_locks.Length; i < newLocks.Length; i++)
                    {
                        newLocks[i] = new object();
                    }
                }

                Node[] newBuckets = new Node[newLength];
                int[] newCountPerLock = new int[newLocks.Length];

                for (int i = 0; i < tables.m_buckets.Length; i++)
                {
                    Node current = tables.m_buckets[i];
                    while (current != null)
                    {
                        Node next = current.m_next;
                        int newBucketNo, newLockNo;
                        int nodeHashCode = current.m_hashcode;

                        if (regenerateHashKeys)
                        {
                            //Recompute the hash from the key
                            nodeHashCode = newComparer.GetHashCode(current.m_key);
                        }

                        GetBucketAndLockNo(nodeHashCode, out newBucketNo, out newLockNo, newBuckets.Length,
                            newLocks.Length);

                        newBuckets[newBucketNo] = new Node(current.m_key, current.m_value, nodeHashCode,
                            newBuckets[newBucketNo]);
                        checked
                        {
                            newCountPerLock[newLockNo]++;
                        }

                        current = next;
                    }
                }

                if (regenerateHashKeys)
                {
                    unchecked
                    {
                        m_keyRehashCount++;
                    }
                }

                m_budget = Math.Max(1, newBuckets.Length / newLocks.Length);

                m_tables = new Tables(newBuckets, newLocks, newCountPerLock, newComparer);
            }
            finally
            {
                ReleaseLocks(0, locksAcquired);
            }
        }

        private void AcquireAllLocks(ref int locksAcquired)
        {
            // First, acquire lock 0
            AcquireLocks(0, 1, ref locksAcquired);

            // Now that we have lock 0, the m_locks array will not change (i.e., grow),
            // and so we can safely read m_locks.Length.
            AcquireLocks(1, m_tables.m_locks.Length, ref locksAcquired);
        }

        private void AcquireLocks(int fromInclusive, int toExclusive, ref int locksAcquired)
        {
            object[] locks = m_tables.m_locks;

            for (int i = fromInclusive; i < toExclusive; i++)
            {
                bool lockTaken = false;
                try
                {
                    Monitor.Enter(locks[i], ref lockTaken);
                }
                finally
                {
                    if (lockTaken)
                        locksAcquired++;
                }
            }
        }

        private void ReleaseLocks(int fromInclusive, int toExclusive)
        {
            for (int i = fromInclusive; i < toExclusive; i++)
            {
                Monitor.Exit(m_tables.m_locks[i]);
            }
        }

        #endregion Private
    }
}