
using System.IO;
using Couchbase.Configuration.Server.Serialization;
using Newtonsoft.Json;
using NUnit.Framework;
using Couchbase.Core;

namespace Couchbase.Tests.Core
{
    [TestFixture]
    public class NodeAdapterExtensionsTests
    {
        [Test]
        public void When_NodeAdapter_Lists_Are_Not_Equal_Return_False()
        {
            var rev839 =
               JsonConvert.DeserializeObject<BucketConfig>(
                   File.ReadAllText("Data\\Configuration\\couchbase-4.0-rev839.json"));

            var rev855 =
             JsonConvert.DeserializeObject<BucketConfig>(
                 File.ReadAllText("Data\\Configuration\\couchbase-4.0-rev855.json"));

            Assert.IsFalse(rev839.GetNodes().AreEqual(rev855.GetNodes()));
        }

        [Test]
        public void When_NodeAdapter_Lists_Are_Not_Equal_Return_False2()
        {
            var rev839 =
               JsonConvert.DeserializeObject<BucketConfig>(
                   File.ReadAllText("Data\\Configuration\\couchbase-4.0-rev839.json"));

            var rev855 =
             JsonConvert.DeserializeObject<BucketConfig>(
                 File.ReadAllText("Data\\Configuration\\couchbase-4.0-rev855.json"));

            Assert.IsFalse(rev855.GetNodes().AreEqual(rev839.GetNodes()));
        }

        [Test]
        public void When_NodeAdapter_Lists_Are_Equal_Return_True()
        {
            var rev855 =
             JsonConvert.DeserializeObject<BucketConfig>(
                 File.ReadAllText("Data\\Configuration\\couchbase-4.0-rev855.json"));

            Assert.IsTrue(rev855.GetNodes().AreEqual(rev855.GetNodes()));
        }

        [Test]
        public void When_NodeAdapter_Other_Is_Null_Return_False()
        {
            var rev855 =
             JsonConvert.DeserializeObject<BucketConfig>(
                 File.ReadAllText("Data\\Configuration\\couchbase-4.0-rev855.json"));

            Assert.IsFalse(rev855.GetNodes().AreEqual(null));
        }

        [Test]
        public void When_NodeAdapter_This_Is_Null_Return_False()
        {
            var rev855 =
             JsonConvert.DeserializeObject<BucketConfig>(
                 File.ReadAllText("Data\\Configuration\\couchbase-4.0-rev855.json"));

            Assert.IsFalse(NodeAdapterExtensions.AreEqual(null, rev855.GetNodes()));
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
