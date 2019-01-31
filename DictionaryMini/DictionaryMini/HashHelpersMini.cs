using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace DictionaryMini
{
    internal static class HashHelpersMini
    {
        //关于Buckets的长度为什么要为质数?
        //https://cs.stackexchange.com/questions/11029/why-is-it-best-to-use-a-prime-number-as-a-mod-in-a-hashing-function/64191#64191
        //Consider the set of keys K = { 0, 1,..., 100 } and a hash table where the number of buckets is m=12. Since 3 is a factor of 12, the keys that are multiples of 3 will be hashed to buckets that are multiples of 3:

        //Keys {0,12,24,36,...}
        //will be hashed to bucket 0.
        //Keys {3,15,27,39,...}
        //will be hashed to bucket 3.
        //Keys {6,18,30,42,...} will be hashed to bucket 6.
        //Keys {9,21,33,45,...} will be hashed to bucket 9.
        //If K is uniformly distributed(i.e., every key in K is equally likely to occur), then the choice of m is not so critical.But, what happens if K is not uniformly distributed? Imagine that the keys that are most likely to occur are the multiples of 3. In this case, all of the buckets that are not multiples of 3 will be empty with high probability (which is really bad in terms of hash table performance).

        //This situation is more common that it may seem. Imagine, for instance, that you are keeping track of objects based on where they are stored in memory. If your computer's word size is four bytes, then you will be hashing keys that are multiples of 4. Needless to say that choosing m to be a multiple of 4 would be a terrible choice: you would have 3m/4 buckets completely empty, and all of your keys colliding in the remaining m/4 buckets.

        //    In general:

        //Every key in K that shares a common factor with the number of buckets m will be hashed to a bucket that is a multiple of this factor.

        //Therefore, to minimize collisions, it is important to reduce the number of common factors between m and the elements of K. How can this be achieved? By choosing m to be a number that has very few factors: a prime number.

        private static readonly int[] primes = {
            3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
            1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
            17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
            187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
            1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369};

        public static bool IsPrime(int candidate)
        {
            if ((candidate & 1) != 0)
            {
                int limit = (int)Math.Sqrt(candidate);
                for (int divisor = 3; divisor <= limit; divisor += 2)
                {
                    if ((candidate % divisor) == 0)
                        return false;
                }
                return true;
            }
            return (candidate == 2);
        }

        public static int GetPrime(int min)
        {
            if (min < 0)
                throw new ArgumentOutOfRangeException();
            for (int i = 0; i < primes.Length; i++)
            {
                int prime = primes[i];
                if (prime >= min) return prime;
            }
            for (int i = (min | 1); i < Int32.MaxValue; i += 2)
            {
                if (IsPrime(i))
                    return i;
            }
            return min;
        }
    }
}