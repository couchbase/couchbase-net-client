using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class ObserveTests : OperationTestBase
    {
        [Test]
        public void Test_Observe()
        {
            const string key = "Test_Observe";

            var operation = new Observe(key, GetVBucket(), Transcoder, OperationLifespanTimeout);
            var result = IOService.Execute(operation);
            Console.WriteLine(result.Message);
            Assert.IsTrue(result.Success);
        }

        [Test]
        public async void Test_Observe2()
        {
            const string key = "Test_Observe2";
            var remove = new Delete(key, GetVBucket(), Transcoder, OperationLifespanTimeout);

            var set = new Set<int?>(key, 10, GetVBucket(), Transcoder, OperationLifespanTimeout);
            var result = IOService.Execute(set);
            Assert.IsTrue(result.Success);

            var get = new Get<dynamic>(key, GetVBucket(), Transcoder, OperationLifespanTimeout);
            var result1 = IOService.Execute(get);
            Assert.IsTrue(result1.Success);
            Assert.AreEqual(result.Cas, result1.Cas);

            await Task.Delay(100);
            var operation = new Observe(key, GetVBucket(), Transcoder, OperationLifespanTimeout);
            var result2 = IOService.Execute(operation);

            Assert.AreEqual(result1.Cas, result2.Value.Cas);

            Assert.AreEqual(KeyState.FoundPersisted, result2.Value.KeyState);
            Assert.IsTrue(result2.Success);
        }

        [Test]
        public void Test_Clone()
        {
            var operation = new Observe("key", GetVBucket(), Transcoder, OperationLifespanTimeout)
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
    }
}
