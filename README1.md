# ConsurrentDictionaryMini

在上一篇文章`你真的了解字典吗(dictionary)?`一文中我介绍了Hash Function和字典的工作的基本原理.
有网友在文章底部评论,说我的Remove和Add方法没有考虑线程安全问题.  
<https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2?redirectedfrom=MSDN&view=netframework-4.7.2>  

在查阅相关资料后,发现只要未对集合进行修改,Dictionary就可以被多个线程同时读取,但是如果需要多个线程同时访问字典进行添加,修改操作,那就必须自己进行加锁了.  