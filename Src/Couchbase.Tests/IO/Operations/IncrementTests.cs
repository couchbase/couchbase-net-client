using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.Tests.Utils;
using Couchbase.Views;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class IncrementTests : OperationTestBase
    {
        private Cluster _cluster;

        [SetUp]
        public override void OneTimeSetUp()
        {
            base.OneTimeSetUp();
            _cluster = new Cluster(ClientConfigUtil.GetConfiguration());
        }

        [Test]
        public void Test_IncrementOperation()
        {
            const string key = "Test_IncrementOperation";

            var delete = new Delete(key, GetVBucket(), Transcoder, OperationLifespanTimeout);
            var result = IOService.Execute(delete);
            Console.WriteLine("Deleting key {0}: {1}", key, result.Success);

            var increment = new Increment(key, 0, 1, 0, GetVBucket(), Transcoder, OperationLifespanTimeout);
            var result1 = IOService.Execute(increment);
            Assert.IsTrue(result1.Success);
            Assert.AreEqual(result1.Value, uint.MinValue);

            increment.Reset();
            var result2 = IOService.Execute(increment);
            Assert.IsTrue(result2.Success);
            Assert.AreEqual(1, result2.Value);

            var getOperation = new Get<string>(key, GetVBucket(), Transcoder, OperationLifespanTimeout);
            var result3 = IOService.Execute(getOperation);
            var value = result3.Value;
            Assert.AreEqual(result2.Value.ToString(CultureInfo.InvariantCulture), result3.Value);
        }

        [Test]
        public void Test_Clone()
        {
            var operation = new Increment("key", 1, 1, 0, GetVBucket(), Transcoder, OperationLifespanTimeout)
            {
                Cas = 1123
            };
            var cloned = operation.Clone();
            Assert.AreEqual(operation.CreationTime, cloned.CreationTime);
            Assert.AreEqual(operation.Cas, cloned.Cas);
            Assert.AreEqual(operation.VBucket.Index, cloned.VBucket.Index);
            Assert.AreEqual(operation.Key, cloned.Key);
            Assert.AreEqual(operation.Opaque, cloned.Opaque);
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
            _cluster.Dispose();
        }
    }
}
