# DictionaryMini

一个基于 .Net Standar 2.0 设计的 字典库.
### 字典为什么能无限地Add呢?
### 从字典中取Item速度非常快,为什么呢?
### 初始化字典有必要指定字典容量吗?
### 字典的 buckets 长度为素数,为什么呢?
### 字典中的Item Remove后如何分配给下一个?
### 给字典添加Item之前一定要判断key是否存在吗?

带着这些疑问,走进DictionoryMini...

###首先,我们要从哈希函数说起.
什么是哈希函数?
哈希函数又称散列函数,是一种从任何一种数据中创建小的数字“指纹”的方法。
下面,我们以JDK中Sting.GetHashCode()的源码看起.

```java
public int hashCode() {
        int h = hash;
 //hash default value : 0 
        if (h == 0 && value.length > 0) {
 //value : char storage
            char val[] = value;

            for (int i = 0; i < value.length; i++) {
                h = 31 * h + val[i];
            }
            hash = h;
        }
        return h;
    }

```
可以看到,无论多长的字符串,最终都会返回一个int值,可以看到,当哈希函数确定的情况下,任何一个字符串的哈希值都是唯一且确定的.
当然,这里只是找了一种最简单的字符数哈希值求法,理论上只要能把一个对象转换成唯一且确定值的函数,我们都可以把它称之为哈希函数.

这是哈希函数的工作原理图.


![Alt text](https://raw.githubusercontent.com/liuzhenyulive/DictionaryMini/master/Pic/HashFunction.svg?sanitize=true)

首先,开局一张图,后面全靠编,我找到到的维基百科中关于HashTable的图片.

![Alt text](https://raw.githubusercontent.com/liuzhenyulive/DictionaryMini/master/Pic/hashtable0.svg?sanitize=true)





![Alt text](https://raw.githubusercontent.com/liuzhenyulive/DictionaryMini/master/Pic/hashtable1.svg?sanitize=true)

![Alt text](https://raw.githubusercontent.com/liuzhenyulive/DictionaryMini/master/Pic/hashtable2.svg?sanitize=true)

![Alt text](https://raw.githubusercontent.com/liuzhenyulive/DictionaryMini/master/Pic/hashtable3.svg?sanitize=true)

