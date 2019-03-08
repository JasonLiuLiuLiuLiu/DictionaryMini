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

