﻿using System;
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

            var operation = new Observe(key, GetVBucket(), new AutoByteConverter());
            var result = IOStrategy.Execute(operation);
            Console.WriteLine(result.Message);
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Test_Observe2()
        {
            const string key = "Test_Observe2";
            var remove = new Delete(key, GetVBucket(), Converter, Transcoder);

            var set = new Set<int?>(key, 10, GetVBucket(), Converter, Transcoder);
            var result = IOStrategy.Execute(set);
            Assert.IsTrue(result.Success);

            var get = new Get<dynamic>(key, GetVBucket(), Converter, Transcoder);
            var result1 = IOStrategy.Execute(get);
            Assert.IsTrue(result1.Success);
            Assert.AreEqual(result.Cas, result1.Cas);

            var operation = new Observe(key, GetVBucket(), new AutoByteConverter());
            var result2 = IOStrategy.Execute(operation);
            Assert.AreEqual(result1.Cas, result2.Value.Cas);

            Assert.AreEqual(KeyState.FoundPersisted, result2.Value.KeyState);
            Assert.IsTrue(result2.Success);
        }

        [Test]
        public void Test_Clone()
        {
            var operation = new Observe("key", GetVBucket(), Converter, Transcoder)
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
