using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core.Serialization;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.UnitTests.Utils
{
    [TestFixture]
    public class ArrayExtensionsTests
    {
        [Test]
        public void Test_GetCombinedHashcode_Jagged_Arrays_Are_NotEqual()
        {
            int[][] array1 = { new[] { 1, -1 }, new[] { 1, -1 }, new[] { 1, -1 } };
            int[][] array2 = { new[] { -1, -1 }, new[] { 1, -1 }, new[] { -1, -1 } };
            Assert.AreNotEqual(array1.GetCombinedHashcode(), array2.GetCombinedHashcode());
        }

        [Test]
        public void Test_GetCombinedHashcode_Jagged_Arrays_Are_Equal()
        {
            int[][] array1 = { new[] { 1, -1 }, new[] { 1, -1 }, new[] { 1, -1 } };
            int[][] array2 = { new[] { 1, -1 }, new[] { 1, -1 }, new[] { 1, -1 } };
            Assert.AreEqual(array1.GetCombinedHashcode(), array2.GetCombinedHashcode());
        }

        [Test]
        public void Test_GetCombinedHashcode_Arrays_Are_NotEqual()
        {
            var array1 = new[] {1, 2, 3};
            var array2 = new[] {2, 3, 4};
            Assert.AreNotEqual(array1.GetCombinedHashcode(), array2.GetCombinedHashcode());
        }

        [Test]
        public void Test_GetCombinedHashcode_Arrays_Are_Equal()
        {
            var array1 = new[] { 1, 2, 3 };
            var array2 = new[] { 1, 2, 3 };
            Assert.AreEqual(array1.GetCombinedHashcode(), array2.GetCombinedHashcode());
        }

        [Test]
        public void Test_AreEqual_Arrays_Are_Equal()
        {
            var array1 = new[] { 1, 2, 3 };
            var array2 = new[] { 1, 2, 3 };
            Assert.IsTrue(array1.AreEqual<int>(array2));
        }

        [Test]
        public void Test_AreEqual_Arrays_Are_Not_Equal()
        {
            var array1 = new[] { 1, 5, 3 };
            var array2 = new[] { 1, 2, 3 };
            Assert.IsFalse(array1.AreEqual<int>(array2));
        }

        [Test]
        public void Test_AreEqual_Two_Dimensional_Arrays_Are_Equal()
        {
            int[][] array1 = { new[]{1, -1}, new[]{1, -1}, new[]{1, -1} };
            int[][] array2 = { new[] { 1, -1 }, new[] { 1, -1 }, new[] { 1, -1 } };
            Assert.IsTrue(array1.AreEqual(array2));
        }

        [Test]
        public void Test_AreEqual_Two_Dimensional_Arrays_Are_Not_Equal()
        {
            int[][] array1 = { new[] { 1, -1 }, new[] { 1, -1 }, new[] { 1, -1 } };
            int[][] array2 = { new[] { -1, 1 }, new[] { 1, -1 }, new[] { -1, 1 } };
            Assert.IsFalse(array1.AreEqual(array2));
        }

        [Test]
        public void Test_AreEqual_Two_Dimensional_Arrays_Are_Not_Equal_Different_Lengths()
        {
            int[][] array1 = { new[] { 1, -1 }, new[] { 1, -1 } };
            int[][] array2 = { new[] { -1, 1 }, new[] { 1, -1 }, new[] { -1, 1 } };
            Assert.IsFalse(array1.AreEqual(array2));
        }

        [Test]
        public void Test_Shuffle_On_List()
        {
            var savedRandom = ArrayExtensions.Random;
            try
            {
                // Use a random number generator with a known seed for consistency
                ArrayExtensions.Random = new Random(500);

                bool different = false;
                var array1 = new List<char> {'a', 'b', 'c', 'd'};
                var array2 = array1.Shuffle();
                for (int i = 0; i < array1.Count; i++)
                {
                    different = array1[i] == array2[i];
                    if (different)
                    {
                        break;
                    }
                }
                Assert.IsTrue(different);
            }
            finally
            {
                ArrayExtensions.Random = savedRandom;
            }
        }

        [Test]
        public void Test_GetRandom()
        {
            var savedRandom = ArrayExtensions.Random;
            try
            {
                // Use a random number generator with a known seed for consistency
                ArrayExtensions.Random = new Random(500);

                var array1 = new List<char> { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p' };
                var random1 = array1.GetRandom();
                var random2 = array1.GetRandom();

                Assert.AreNotEqual(random1, random2);
            }
            finally
            {
                ArrayExtensions.Random = savedRandom;
            }
        }

        [Test]
        public void Test_GetRandom_IEnumerable()
        {
            var savedRandom = ArrayExtensions.Random;
            try
            {
                // Use a random number generator with a known seed for consistency
                ArrayExtensions.Random = new Random(500);

                var array1 = new List<char> { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p' };
                var random1 = array1.Where(x=>x !='c').GetRandom();
                var random2 = array1.Where(x => x != 'c').GetRandom();

                Assert.AreNotEqual(random1, random2);
            }
            finally
            {
                ArrayExtensions.Random = savedRandom;
            }
        }

        [Test]
        public void When_Length_Is_One_Return_Same_Item()
        {
            var array1 = new List<char> { 'b',};
            var random1 = array1.GetRandom();
            var random2 = array1.GetRandom();

            Assert.AreEqual(random1, random2);
        }

        [Test]
        public void When_Length_Is_One_Return_Same_Item_IEnumerable()
        {
            var array1 = new List<char> { 'b', };
            var random1 = array1.Where(x => x != 'c').GetRandom();
            var random2 = array1.Where(x => x != 'c').GetRandom();

            Assert.AreEqual(random1, random2);
        }

        [Test]
        public void When_Length_Is_Zero_Return_Default_T()
        {
            var array1 = new List<char> { };
            var random1 = array1.GetRandom();
            var random2 = array1.GetRandom();

            Assert.AreEqual('\0', random1);
            Assert.AreEqual(random1, random2);
        }

        [Test]
        public void When_Length_Is_Zero_Return_Default_T2()
        {
            var array1 = new List<object> { };
            var random1 = array1.Where(x => x != null).GetRandom();
            var random2 = array1.GetRandom();

            Assert.AreEqual(null, random1);
            Assert.AreEqual(random1, random2);
        }

        [Test]
        public void StripBrackets_IntList_RemovesBrackets()
        {
            //arrange
            var serializer = new DefaultSerializer();
            object values = new List<int> {1, 2, 3, 4};

            //act
            var valuesBytes = serializer.Serialize(values);
            var actual = valuesBytes.StripBrackets();

            //assert
            var expected = new byte[] { 49, 44, 50, 44, 51, 44, 52 };
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void StripBrackets_StringList_RemovesBrackets()
        {
            //arrange
            var serializer = new DefaultSerializer();
            object values = new List<string> { "1", "2", "3", "4" };

            //act
            var valuesBytes = serializer.Serialize(values);
            var actual = valuesBytes.StripBrackets();

            //assert
            var expected = new byte[] {0x22, 0x31, 0x22, 0x2c, 0x22, 0x32, 0x22, 0x2c, 0x22, 0x33, 0x22, 0x2c, 0x22, 0x34, 0x22};
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void StripBrackets_EmptyList_RemovesBrackets()
        {
            //arrange
            var serializer = new DefaultSerializer();
            object values = new List<int> { };

            //act
            var valuesBytes = serializer.Serialize(values);
            var actual = valuesBytes.StripBrackets();

            //assert
            var expected = new byte[] { };
            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void StripBrackets_WhenString_DoesNothing()
        {
            //arrange
            var serializer = new DefaultSerializer();
            object values = "howdy pardner";

            //act
            var valuesBytes = serializer.Serialize(values);
            var actual = valuesBytes.StripBrackets();

            //assert
            var expected = new byte[] {
                0x22, 0x68, 0x6f, 0x77, 0x64, 0x79, 0x20, 0x70, 0x61, 0x72, 0x64, 0x6e, 0x65, 0x72, 0x22 };
            Assert.AreEqual(expected, actual);
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