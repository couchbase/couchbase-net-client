using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    public class AppendOperationTests : OperationTestBase
    {
        [Test]
        public void When_Key_Exists_Append_Succeeds()
        {
            const string key = "Hello";
            const string expected = "Hello!";

            //clean up old keys
            var deleteOperation = new Delete(key, GetVBucket(), Converter, Transcoder);
            IOStrategy.Execute(deleteOperation);

            deleteOperation = new Delete(key + "!", GetVBucket(),  Converter, Transcoder);
            IOStrategy.Execute(deleteOperation);

            //create the key
            var set = new Set<string>(key, "Hello", GetVBucket(), Converter);
            var addResult = IOStrategy.Execute(set);
            Assert.IsTrue(addResult.Success);

            var append = new Append<string>(key, "!", Transcoder, GetVBucket(), Converter);
            var result = IOStrategy.Execute(append);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(string.Empty, result.Value);

            var get = new Get<string>(key, GetVBucket(),  Converter, Transcoder);
            var getResult = IOStrategy.Execute(get);
            Assert.AreEqual(expected, getResult.Value);
        }

        [Test]
        public void Test_Clone()
        {
            var operation = new Append<string>("Hello", "!", Transcoder, GetVBucket(), Converter);
            var cloned = operation.Clone();
            Assert.AreEqual(operation.CreationTime, cloned.CreationTime);
            Assert.AreEqual(operation.Cas, cloned.Cas);
            Assert.AreEqual(operation.VBucket.Index, cloned.VBucket.Index);
            Assert.AreEqual(operation.Key, cloned.Key);
            Assert.AreEqual(operation.Opaque, cloned.Opaque);
            Assert.AreEqual(operation.RawValue, ((OperationBase<string>)cloned).RawValue);
        }
    }
}
