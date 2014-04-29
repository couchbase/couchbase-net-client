using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class SetOperationTests
    {
        private Cluster _cluster;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            Cluster.Initialize();
            _cluster = Cluster.Get();
        }

        [Test]
        public void Test_Insert_String()
        {
            using (var bucket = _cluster.OpenBucket("default"))
            {
                var response = bucket.Insert(TestKeys.KeyWithStringValue.Key, TestKeys.KeyWithStringValue.Value);
                Assert.IsTrue(response.Success);
            }
        }

        [Test]
        public void Test_Insert_Int32()
        {
            using (var bucket = _cluster.OpenBucket("default"))
            {
                var response = bucket.Insert(TestKeys.KeyWithInt32Value.Key, TestKeys.KeyWithInt32Value.Value);
                Assert.IsTrue(response.Success);
            }
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            _cluster.Dispose();
        }
    }
}
