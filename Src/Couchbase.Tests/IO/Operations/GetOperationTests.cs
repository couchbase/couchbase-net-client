using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Awaitable;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class GetOperationTests
    {
        private Cluster _cluster;

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            _cluster = new Cluster();
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

        [TearDown]
        public void TearDown()
        {
            
        }

        [TestFixtureTearDown]
        public void TestFixtureTearDown()
        {
            _cluster.Dispose();
        }
    }
}
