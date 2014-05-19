using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.Tests.Utils
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