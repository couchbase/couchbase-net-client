using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

#nullable enable

namespace Couchbase.Utils
{
    internal static class ArrayExtensions
    {
#if NETCOREAPP3_1_OR_GREATER
        private static int GetRandomInt32(int toExclusive) => RandomNumberGenerator.GetInt32(toExclusive);
#else
        /// <summary>
        /// Provides random number generation for array randomization
        /// </summary>
        private static readonly Random Random = new();

        private static int GetRandomInt32(int toExclusive) => Random.Next(toExclusive);
#endif

        public static List<T> Shuffle<T>(this List<T> list)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (list is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(list));
            }

            var length = list.Count;
            while (length > 1)
            {
                var index = GetRandomInt32(length);
                length--;
                (list[index], list[length]) = (list[length], list[index]);
            }
            return list;
        }

        public static T? RandomOrDefault<T>(this IEnumerable<T> source)
        {
            if (source is IList<T> list)
            {
                // Fast path for a known length

                if (list.Count == 0)
                {
                    return default;
                }

                return list[GetRandomInt32(list.Count)];
            }

            var item = default(T);

            var count = 0;
            foreach (var element in source)
            {
                if (++count == 1)
                {
                    item = element; // 1st matching.
                }
                else
                {
                    // If more than one item is an option, apply a weighted random selection.
                    // For example, if this is the 4th item, the current value of item is one
                    // of the first 3 items. We should therefore have a 1 in 4 chance of selecting
                    // the current item, otherwise leave the previous random selection from the
                    // first 3 items.
                    // the argument to GetRandomInt32 is the exclusive upper-bound
                    // 2nd matching, count==2, 1/2  GetRandomInt32(2) → (0..1) 1/2
                    // 3rd matching, count==3, 1/3  GetRandomInt32(3) → (0..2) 1/3
                    // 4th matching, count==4, 1/4  GetRandomInt32(4) → (0..3) 1/4

                    if (GetRandomInt32(count) == 0)
                    {
                        item = element;
                    }
                }
            }

            return item;
        }

        // The overload with a predicate could delegate to the overload without a predicate using call to Enumerable.Where,
        // but this would add an extra layer of indirection and heap allocation. Alternatively, we could delegate from the
        // other overload to this one and pass an always-true predicate, but that adds the overhead of a delegate call.
        // So repeating the implementation is the most efficient solution.
        public static T? RandomOrDefault<T>(this IEnumerable<T> source, Func<T, bool> predicate)
        {
            var item = default(T);

            var count = 0;
            foreach (var element in source)
            {
                if (predicate(element))
                {
                    if (++count == 1)
                    {
                        item = element; // 1st matching
                    }
                    else
                    {
                        // If more than one item is an option, apply a weighted random selection.
                        // For example, if this is the 4th item, the current value of item is one
                        // of the first 3 items. We should therefore have a 1 in 4 chance of selecting
                        // the current item, otherwise leave the previous random selection from the
                        // first 3 items.
                        // the argument to GetRandomInt32 is the exclusive upper-bound
                        // 2nd matching, count==2, 1/2  GetRandomInt32(2) → (0..1) 1/2
                        // 3rd matching, count==3, 1/3  GetRandomInt32(3) → (0..2) 1/3
                        // 4th matching, count==4, 1/4  GetRandomInt32(4) → (0..3) 1/4

                        if (GetRandomInt32(count) == 0)
                        {
                            item = element;
                        }
                    }
                }
            }

            return item;
        }

        public static bool AreEqual<T>(this List<T>? array, List<T>? other)
        {
            if (ReferenceEquals(array, other))
            {
                // They are the same instance or are both null
                return true;
            }
            if (array == null || other == null)
            {
                return false;
            }

#if NET6_0_OR_GREATER
            // For modern frameworks, this can allow vectorization if the type T is bitwise equatable.
            // This won't be true if T is a reference type, but may occur for value types.
            return CollectionsMarshal.AsSpan(array).SequenceEqual(CollectionsMarshal.AsSpan(other));
#else
            return array.SequenceEqual(other);
#endif
        }

        public static bool AreEqual<T>(this T[]? array, T[]? other)
        {
            if (ReferenceEquals(array, other))
            {
                // They are the same instance or are both null
                return true;
            }
            if (array == null || other == null)
            {
                return false;
            }

            return array.SequenceEqual(other);
        }

        public static bool AreEqual(this short[]?[]? array, short[]?[]? other)
        {
            if (ReferenceEquals(array, other))
            {
                // They are the same instance or are both null
                return true;
            }
            if (array == null || other == null)
            {
                return false;
            }
            if (array.Length != other.Length)
            {
                return false;
            }

            for (var i = 0; i < array.Length; i++)
            {
                var innerArray = array[i];
                var innerOther = other[i];

                if (ReferenceEquals(innerArray, innerOther))
                {
                    // They are the same instance or are both null
                    continue;
                }
                if (innerArray == null || innerOther == null)
                {
                    return false;
                }

                // Vectorized comparison where possible, for short[] even .NET 4 can vectorize when using MemoryExtensions.SequenceEqual on a ReadOnlySpan<short>.
                if (!((ReadOnlySpan<short>)innerArray).SequenceEqual(innerOther))
                {
                    return false;
                }
            }

            return true;
        }

        public static int GetCombinedHashcode<T>(this T[] array)
            where T : notnull
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (array is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(array));
            }

            var hashCode = new HashCode();

            foreach (var item in array)
            {
                hashCode.Add(item);
            }

            hashCode.Add(array.Length);

            return hashCode.ToHashCode();
        }

        // Overload used for VBucketServerMaps
        public static int GetCombinedHashcode(this short[][] array)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (array is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(array));
            }

            var hashCode = new HashCode();

            var count = 0;
            foreach (var innerArray in array)
            {
#if NET6_0_OR_GREATER
                // It isn't important that the hash code be consistent across platforms, only that it is consistent
                // for a given application execution. On newer platforms, use a more performant hash code calculation
                // since we can safely consider an array of shorts as an array of bytes.
                hashCode.AddBytes(MemoryMarshal.AsBytes((ReadOnlySpan<short>)innerArray));
#else
                foreach (var item in innerArray)
                {
                    hashCode.Add(item);
                }
#endif

                count += innerArray.Length;
            }

            hashCode.Add(count);

            return hashCode.ToHashCode();
        }

        public static ReadOnlyMemory<byte> StripBrackets(this ReadOnlyMemory<byte> theArray)
        {
            if (theArray.Length > 1 && theArray.Span[0] == 0x5b && theArray.Span[theArray.Length-1] == 0x5d)
            {
                return theArray.Slice(1, theArray.Length - 2);
            }
            return theArray;
        }

        public static bool IsJson(this Span<byte> buffer) =>
            ((ReadOnlySpan<byte>) buffer).IsJson();

        public static bool IsJson(this ReadOnlySpan<byte> buffer) =>
            buffer.Length > 1 &&
            ((buffer[0] == 0x5b && buffer[buffer.Length - 1] == 0x5d) ||
             (buffer[0] == 0x7b && buffer[buffer.Length - 1] == 0x7d));
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
