﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Utils
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
            return list;
        }

        public static T GetRandom<T>(this List<T> list)
        {
            T item;
            var length = list.Count;
            if (length > 0)
            {
                var index = Random.Next(length);
                item = list[index];
            }
            else
            {
                item = default(T);
            }
            return item;
        }

        public static T GetRandom<T>(this IEnumerable<T> list)
        {
            T item;
            var enumerable = list as IList<T> ?? list.ToList();
            var length = enumerable.Count();
            if (length > 0)
            {
                var index = Random.Next(length);
                item = enumerable[index];
            }
            else
            {
                item = default(T);
            }
            return item;
        }

        public static bool AreEqual<T>(this Array array, Array other)
        {
            return (other != null &&
                CompareItems<T>(array, other));
        }

        static bool CompareItems<T>(this Array array, Array other)
        {
            return array.Rank == other.Rank &&
                   Enumerable.Range(0, array.Rank).
                   All(dim => array.GetLength(dim) == other.GetLength(dim)) &&
                   array.Cast<T>().SequenceEqual(other.Cast<T>());
        }

        public static bool AreEqual(this int[][] array, int[][] other)
        {
            if (array.Length != other.Length)
            {
                return false;
            }

            for (var i = 0; i < array.Length; i++)
            {
                for (var j = 0; j < array[j].Length; j++)
                {
                    if (array[i][j] != other[i][j])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public static int GetCombinedHashcode(this Array array)
        {
            unchecked
            {
                var hash = 0;
                var count = 0;
                foreach (var item in array)
                {
                    if (item.GetType().BaseType == typeof (Array))
                    {
                        var jagged = (Array)item;
                        foreach (var inner in jagged)
                        {
                            hash += inner.GetHashCode();
                            count++;
                        }
                    }
                    else
                    {
                        hash += item.GetHashCode();
                        count++;
                    }
                }
                return 31*hash + count.GetHashCode();
            }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion