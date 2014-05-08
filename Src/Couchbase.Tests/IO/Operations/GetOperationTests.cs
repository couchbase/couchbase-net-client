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
            CouchbaseCluster.Initialize();
            _cluster = CouchbaseCluster.Get();
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
                var setResponse = bucket.Insert(key, value);
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
            _cluster.Dispose();
        }
    }
}