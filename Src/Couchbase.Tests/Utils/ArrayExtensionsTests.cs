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
