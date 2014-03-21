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
    public class BoolExtensionTests
    {
        [Test]
        public void Test_That_ToLowerString_Returns_false_When_Null_On_Nullable_bool()
        {
            bool? value =null;
            Assert.AreEqual("false", value.ToLowerString());
        }

        [Test]
        public void Test_That_ToLowerString_Returns_true_When_True_On_Nullable_bool()
        {
            bool? value = true;
            Assert.AreEqual("true", value.ToLowerString());
        }

        [Test]
        public void Test_That_ToLowerString_Returns_true_When_False_On_Nullable_bool()
        {
            bool? value = false;
            Assert.AreEqual("false", value.ToLowerString());
        }

        [Test]
        public void Test_That_ToLowerString_Returns_false_When_False()
        {
            bool value = false;
            Assert.AreEqual("false", value.ToLowerString());
        }

        [Test]
        public void Test_That_ToLowerString_Returns_true_When_True()
        {
            bool value = true;
            Assert.AreEqual("true", value.ToLowerString());
        }
    }
}
