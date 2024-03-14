using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Reflection;
using System.Security.Cryptography;

namespace Couchbase.Utils
{
    internal static class ArrayExtensions
    {
        /// <summary>
        /// Provides random number generation for array randomization
        /// </summary>
        internal static Random Random = new Random();

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

#nullable enable
        public static T? GetRandom<T>(this IEnumerable<T> list)
            where T : class
        {
            var item = default(T);

            var enumerable = list as IList<T> ?? list.ToList();
            if (enumerable.Count > 0)
            {
#if NETCOREAPP3_1_OR_GREATER
                var index = RandomNumberGenerator.GetInt32(enumerable.Count);
#else
                var index = Random.Next(enumerable.Count);
#endif
                item = enumerable[index];
            }

            return item;
        }

        public static T? GetRandomValueType<T>(this IEnumerable<T> list)
            where T : struct
        {
            T? item = null;

            var enumerable = list as IList<T> ?? list.ToList();
            if (enumerable.Count > 0)
            {
#if NETCOREAPP3_1_OR_GREATER
                var index = RandomNumberGenerator.GetInt32(enumerable.Count);
#else
                var index = Random.Next(enumerable.Count);
#endif
                item = enumerable[index];
            }

            return item;
        }
#nullable restore

        public static T GetRandom<T>(this IEnumerable<T> enumerable, Func<T, bool> whereClause)
        {
            var item = default(T);

            var list = enumerable.Where(whereClause).ToList();
            if (list.Any())
            {
#if NETCOREAPP3_1_OR_GREATER
                var index = RandomNumberGenerator.GetInt32(list.Count);
#else
                var index = Random.Next(list.Count);
#endif
                item = list[index];
            }

            return item;
        }

        public static bool AreEqual<T>(this List<T> array, List<T> other)
        {
            if (array == null && other == null) return true;
            if (array == null) return false;
            if (other == null) return false;

            return array.Count == other.Count && array.SequenceEqual(other);
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

        public static bool AreEqual(this short[][] array, short[][] other)
        {
            if (array == null && other == null)
            {
                return true;
            }
            if (array != null && other == null)
            {
                return false;
            }

            if (array?.Length != other.Length)
            {
                return false;
            }

            for (var i = 0; i < array.Length; i++)
            {
                if (array[i] == null && other[i] == null)
                {
                    continue;
                }

                if (array[i] != null && other[i] == null)
                {
                    return false;
                }

                if (array[i]?.Length != other[i].Length)
                {
                    return false;
                }

                for (var j = 0; j < array[i].Length; j++)
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
                    if (item.GetType().GetTypeInfo().BaseType == typeof (Array))
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

        public static ReadOnlyMemory<byte> StripBrackets(this ReadOnlyMemory<byte> theArray)
        {
            if (theArray.Length > 1 && theArray.Span[0] == 0x5b && theArray.Span[theArray.Length-1] == 0x5d)
            {
                return theArray.Slice(1, theArray.Length - 2);
            }
            return theArray;
        }

        public static bool IsJson(this Span<byte> buffer)
        {
            return ((ReadOnlySpan<byte>) buffer).IsJson();
        }

        public static bool IsJson(this ReadOnlySpan<byte> buffer)
        {
            return (buffer.Length > 1 && buffer[0] == 0x5b && buffer[buffer.Length-1] == 0x5d) ||
                   (buffer.Length > 1 && buffer[0] == 0x7b && buffer[buffer.Length-1] == 0x7d);
        }
    }
}



/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
