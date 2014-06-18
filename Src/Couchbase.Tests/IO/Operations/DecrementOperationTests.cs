using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.Views;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class DecrementOperationTests : OperationTestBase
    {
        private CouchbaseCluster _cluster;

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            CouchbaseCluster.Initialize();
            _cluster = CouchbaseCluster.Get();
        }

        [Test]
        public void Test_DecrementOperation()
        {
            const string key = "Test_DecrementOperation";

            //delete key if exists
            var delete = new DeleteOperation(key, GetVBucket(), new ManualByteConverter());
            var result = IOStrategy.Execute(delete);
            Console.WriteLine("Deleting key {0}: {1}", key, result.Success);

            //increment the key
            var operation = new IncrementOperation(key, 1, 1, 0, GetVBucket(), new ManualByteConverter());
            var result1 = IOStrategy.Execute(operation);
            Assert.IsTrue(result1.Success);
            Assert.AreEqual(result1.Value, 1);

            //key should be 1
            var get = new GetOperation<string>(key, GetVBucket(), new ManualByteConverter());
            var result3 = IOStrategy.Execute(get);
            Assert.AreEqual(result1.Value.ToString(CultureInfo.InvariantCulture), result3.Value);

            //decrement the key
            var decrement = new DecrementOperation(key, 1, 1, 0, GetVBucket(), new ManualByteConverter());
            var result2 = IOStrategy.Execute(decrement);
            Assert.IsTrue(result2.Success);
            Assert.AreEqual(result2.Value, 0);

            //key should be 0
            get = new GetOperation<string>(key, GetVBucket(), new ManualByteConverter());
            result3 = IOStrategy.Execute(get);
            Assert.AreEqual(0.ToString(CultureInfo.InvariantCulture), result3.Value);
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            base.TestFixtureTearDown();
            _cluster.Dispose();
        }
    }
}
