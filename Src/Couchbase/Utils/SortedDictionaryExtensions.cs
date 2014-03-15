using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Utils
{
    public static class SortedDictionaryExtensions
    {
        public static long FindCeilingKey<TK, TV>(this SortedDictionary<TK, TV> dictionary, TK key)
        {
            var keys = dictionary.Keys.ToArray();
            var index = Array.BinarySearch(keys, key);
            return index;
        }

        public static long FindCeilingKey<TK, TV>(this ILookup<TK, TV> dictionary, TK key)
        {
            var keys = dictionary.First();

            var index = 0;//Array.BinarySearch(keys, key);
            return index;
        }
    }
}
