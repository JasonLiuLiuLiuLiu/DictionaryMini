using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace DictionaryMini
{
    public class DictionaryMini<TKey, TValue>
    {
        public ICollection<TKey> Keys { get; }
        public int Count { get; }
        public bool IsReadOnly { get; }
        public ICollection<TValue> Values { get; }

        public int[] buckets;

        private Entry[] entries;

        private int freeList;

        private struct Entry
        {
            public int hashCode;
            public int next;
            public TKey key;
            public TValue value;
        }

        private IEqualityComparer<TKey> comparer;

        public DictionaryMini(int capacity, IEqualityComparer<TKey> comparer)
        {
            if (capacity < 0)
                throw new ArgumentOutOfRangeException();
            if (capacity > 0)
                Initialize(capacity);
            this.comparer = comparer ?? EqualityComparer<TKey>.Default;
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            throw new NotImplementedException();
        }

        public void Add(TKey key, TValue value)
        {
            throw new NotImplementedException();
        }

        public bool ContainsKey(TKey key)
        {
            throw new NotImplementedException();
        }

        public bool Remove(TKey key)
        {
            throw new NotImplementedException();
        }

        public TValue this[TKey key]
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        #region Private

        public void Initialize(int capacity)
        {
            int size = HashHelpersMini.GetPrime(capacity);
            buckets = new int[size];
            for (int i = 0; i < buckets.Length; i++)
            {
                buckets[i] = -1;
            }

            entries = new Entry[size];

            freeList = -1;
        }

        #endregion Private
    }
}