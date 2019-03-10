在上一篇文章`你真的了解字典吗(dictionary)?`一文中我介绍了Hash Function和字典的工作的基本原理.
有网友在文章底部评论,说我的Remove和Add方法没有考虑线程安全问题.  
<https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2?redirectedfrom=MSDN&view=netframework-4.7.2>  

查阅相关资料后,发现字典.net中Dictionary本身时不支持线程安全的,如果要想使用支持线程安全的字典,那么我们就要使用ConcurrentDictionary了.
在研究ConcurrentDictionary的源码后,我觉得在ConcurrentDictionary的线程安全的解决思路我们还可以推广到其他高并发场景,所以,我又来分享我的学习成果了.  

# ConsurrentDictionary

ConsurrentDictionary是可由多个线程同时访问的线程安全的Dictionary,位于System.Collections.Concurrent的命名空间下,该命名空间下除了有ConsurrentDictionary,还有以下Class都是我们常用的那些类库的线程安全版本.

| Class | function |
|--|--|
| [BlockingCollection<T>](https://docs.microsoft.com/zh-cn/dotnet/api/system.collections.concurrent.blockingcollection-1?view=netframework-4.7.2) |  为实现  [IProducerConsumerCollection<T>](https://docs.microsoft.com/zh-cn/dotnet/api/system.collections.concurrent.iproducerconsumercollection-1?view=netframework-4.7.2)  的线程安全集合提供阻塞和限制功能。|
|[ConcurrentBag<T>](https://docs.microsoft.com/zh-cn/dotnet/api/system.collections.concurrent.concurrentbag-1?view=netframework-4.7.2)|表示对象的线程安全的无序集合。|
|[ConcurrentQueue<T>](https://docs.microsoft.com/zh-cn/dotnet/api/system.collections.concurrent.concurrentqueue-1?view=netframework-4.7.2)|表示线程安全的先进先出 (FIFO) 集合。|

在上一篇文章介绍了字典`Dictionary`的基本工作原理后,对这个`ConsurrentDictionary`的原理应该也不难理解,但它又不是简简单单地读写加个`lock`那么简单,他是如何实现高效的线程安全的呢?

# 工作原理

我们回忆一下字典,  在keys与实际存储数据的entries中间,有个buckets作为桥梁,通过hash function获取了key的哈希值后,对这个哈希值进行取余,余数作为buckets的index,而buckets的value就是这个key对应的entry所在的索引,整个过程的时间复杂度为1.  
![Alt text](https://raw.githubusercontent.com/liuzhenyulive/DictionaryMini/master/Pic/hashtable1.svg?sanitize=true)  

ConsurrentDictionary的数据存储类似,只是buckets除了有dictionary中的buckets中桥梁的作用外,还有了存储数据的职责.  
![Alt text](https://raw.githubusercontent.com/liuzhenyulive/DictionaryMini/master/Pic/ConsurrentDictionary.png?sanitize=true)  

## Node

ConcurrentDictionary中的单个数据存储在Node中.  

``` C#
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
```

## Table

而整个ConcurrentDictionary的数据其实就是存储在这样的一个Table中.

``` C#
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
```

在构造函数中创建默认的Table

``` C#
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
```

## Add

向table中添加元素.

``` C#
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
                    // If the number of elements guarded by this lock has exceeded the budget, resize the bucket table.
                    // It is also possible that GrowTable will increase the budget but won't resize the bucket table.
                    // That happens if the bucket table is found to be poorly utilized due to a bad hash function.
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
```

## Get

从Table中获取元素

``` C#
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
```

## Grow table

Table的扩容

``` C#
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
```