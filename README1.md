# ConsurrentDictionaryMini

在上一篇文章`你真的了解字典吗(dictionary)?`一文中我介绍了Hash Function和字典的工作的基本原理.
有网友在文章底部评论,说我的Remove和Add方法没有考虑线程安全问题.  
<https://docs.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2?redirectedfrom=MSDN&view=netframework-4.7.2>  
在查阅了相关资料后,发现只要未修改集合，Dictionary就可以同时支持多个读取器。即便如此，通过集合枚举本质上不是一个线程安全的过程。在枚举与写访问争用的极少数情况下，必须在整个枚举期间锁定该集合。要允许多个线程访问集合以进行读写，您必须实现自己的同步。