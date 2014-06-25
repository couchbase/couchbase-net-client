using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Serializers;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.Views;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class IncrementTests : OperationTestBase
    {
        private CouchbaseCluster _cluster;

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            _cluster = new CouchbaseCluster();
        }

        [Test]
        public void Test_IncrementOperation()
        {
            const string key = "Test_IncrementOperation";

            var delete = new DeleteOperation(key, GetVBucket(), new ManualByteConverter(), new TypeSerializer(new ManualByteConverter()));
            var result = IOStrategy.Execute(delete);
            Console.WriteLine("Deleting key {0}: {1}", key, result.Success);

            var incrementOperation = new IncrementOperation(key, 0, 1, 0, GetVBucket(), new ManualByteConverter(), new TypeSerializer(new ManualByteConverter()));
            var result1 = IOStrategy.Execute(incrementOperation);
            Assert.IsTrue(result1.Success);
            Assert.AreEqual(result1.Value, uint.MinValue);

            var result2 = IOStrategy.Execute(incrementOperation);
            Assert.IsTrue(result2.Success);
            Assert.AreEqual(result2.Value, 1);

            var getOperation = new GetOperation<string>(key, GetVBucket(), new ManualByteConverter(), new TypeSerializer(new ManualByteConverter()));
            var result3 = IOStrategy.Execute(getOperation);
            Assert.AreEqual(result1.Value.ToString(CultureInfo.InvariantCulture), result3.Value);
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            base.TestFixtureTearDown();
            _cluster.Dispose();
        }
    }
}
