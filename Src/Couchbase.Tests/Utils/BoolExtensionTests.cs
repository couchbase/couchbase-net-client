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
            const bool value = false;
            Assert.AreEqual("false", value.ToLowerString());
        }

        [Test]
        public void Test_That_ToLowerString_Returns_true_When_True()
        {
            const bool value = true;
            Assert.AreEqual("true", value.ToLowerString());
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