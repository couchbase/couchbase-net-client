using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class GetOperationTests
    {
        private CouchbaseCluster _cluster;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            _cluster = new CouchbaseCluster();

            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Upsert(TestKeys.KeyWithInt32Value.Key, TestKeys.KeyWithInt32Value.Value);
            }
        }

        [Test]
        public void Test_Get_String()
        {
            using (var bucket = _cluster.OpenBucket("default"))
            {
                var response = bucket.Get<string>(TestKeys.KeyWithStringValue.Key);
                Assert.IsTrue(response.Success);
                Assert.AreEqual(TestKeys.KeyWithStringValue.Value, response.Value);
            }
        }

        [Test]
        public void Test_Get_Int32()
        {
            using (var bucket = _cluster.OpenBucket("default"))
            {
                var response = bucket.Get<int>(TestKeys.KeyWithInt32Value.Key);
                Assert.IsTrue(response.Success);
                Assert.AreEqual(TestKeys.KeyWithInt32Value.Value, response.Value);
            }
        }

        [Test]
        public void When_Get_Follows_Set_Operation_Is_Correct()
        {
            const string key = "getsetkey";
            const string value = "the value";
            using (var bucket = _cluster.OpenBucket("default"))
            {
                var setResponse = bucket.Upsert(key, value);
                Assert.IsTrue(setResponse.Success);

                var getResponse = bucket.Get<string>(key);
                Assert.IsTrue(getResponse.Success);
                Assert.AreEqual(value, getResponse.Value);
            }
        }

        [Test]
        public void When_Key_Not_Found_Success_Is_False()
        {
            const string keyThatDoesntExist = "keyThatDoesntExist";
            using (var bucket = _cluster.OpenBucket("default"))
            {
                var getResponse = bucket.Get<string>(keyThatDoesntExist);
                Assert.IsFalse(getResponse.Success);
                Assert.AreEqual("Not found", getResponse.Message);
            }
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            using (var bucket = _cluster.OpenBucket())
            {
                bucket.Remove(TestKeys.KeyWithInt32Value.Key);
            }
            _cluster.Dispose();
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