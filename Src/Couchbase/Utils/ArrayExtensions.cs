using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Reflection;

namespace Couchbase.Utils
{
    public static class ArrayExtensions
    {
        /// <summary>
        /// Provides random number generation for array randomization
        /// </summary>
        internal static Random Random = new Random();

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
            if (array == null && other == null)
            {
                return true;
            }
            if (array != null && other == null)
            {
                return false;
            }
            if (array == null && other != null)
            {
                return false;
            }
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

        /// <summary>
        /// Converts an array to a JSON string.
        /// </summary>
        /// <param name="array">The array.</param>
        /// <returns></returns>
        public static string ToJson(this IEnumerable array)
        {
            return JsonConvert.SerializeObject(array);
        }

        /// <summary>
        /// Converts an array to a JSON string and optionally strips the begining and ending brackets.
        /// </summary>
        /// <param name="array">The array.</param>
        /// <param name="stripBrackets">if set to <c>true</c> the brackets '[' and ']' will be removed.</param>
        /// <returns>A JSON string array.</returns>
        public static string ToJson(this IEnumerable array, bool stripBrackets)
        {
            if (stripBrackets)
            {
                return ToJson(array).TrimStart('[').TrimEnd(']');
            }
            return ToJson(array);
        }

        public static byte[] StripBrackets(this byte[] theArray)
        {
            if (theArray.Length > 1 && theArray[0] == 0x5b && theArray[theArray.Length-1] == 0x5d)
            {
                var newArray = new byte[theArray.Length - 2];
                Buffer.BlockCopy(theArray, 1, newArray, 0, theArray.Length - 2);
                return newArray;
            }
            return theArray;
        }

        public static bool IsJson(this byte[] theArray, int startIndex, int endIndex)
        {
            if (endIndex < theArray.Length)
            {
                return false;
            }
            return (theArray.Length > 1 && theArray[startIndex] == 0x5b && theArray[endIndex] == 0x5d) ||
                   (theArray.Length > 1 && theArray[startIndex] == 0x7b && theArray[endIndex] == 0x7d);
        }

        /// <summary>Creates a string from a list with each value delimited by the value of <see cref="delimiter"/> and
        /// each value "N1QL escaped" by backticks "`".
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="theArray">The array to construct the string from.</param>
        /// <param name="delimiter">The value to delimit each value by.</param>
        /// <returns>A string of the values of the array delimited by the <see cref="delimiter"/> and enclosed with backticks.</returns>
        // ReSharper disable once InconsistentNaming
        public static string ToDelimitedN1QLString<T>(this T[] theArray, char delimiter)
        {
            var theString = string.Empty;
            for (var i = 0; i < theArray.Length; i++)
            {
                theString += theArray[i].ToString().N1QlEscape();
                if (i != theArray.Length - 1)
                {
                    theString += string.Concat(delimiter, " ");
                }
            }
            return theString;
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