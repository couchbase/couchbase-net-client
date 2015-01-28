using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Couchbase.Extensions
{
    public static class ArrayExtensions
    {
        private static readonly Random Random = new Random();

        public static void Shuffle(this Array array)
        {
            var length = array.Length;
            while (length > 1)
            {
                length--;
                var index = Random.Next(length + 1);
                var item = array.GetValue(index);
                array.SetValue(array.GetValue(length), index);
                array.SetValue(item, length);
            }
        }

        public static List<T> Shuffle<T>(this List<T> list)
        {
            var length = list.Count;
            while (length > 1)
            {
                length--;
                var index = Random.Next(length + 1);
                var item = list[index];
                list[index] = list[length];
                list[length] = item;
            }
            return list.ToList();
        }
    }
}