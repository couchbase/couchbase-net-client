using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class ReplaceTests : OperationTestBase
    {
        [Test]
        public void When_Document_Exists_Replace_Succeeds()
        {
            const string key = "Replace.When_Document_Exists_Replace_Succeeds";

            //delete the value if it exists
            var delete = new Delete(key, GetVBucket(), Converter, Transcoder);
            var result = IOStrategy.Execute(delete);
            Console.WriteLine(result.Message);

            //add the new doc
            var add = new Add<dynamic>(key, new { foo = "foo" }, GetVBucket(), Converter, Transcoder);
            var result1 = IOStrategy.Execute(add);
            Assert.IsTrue(result1.Success);

            //replace it the old doc with a new one
            var replace = new Replace<dynamic>(key, new { bar = "bar" }, GetVBucket(), Converter, Transcoder);
            var result2 = IOStrategy.Execute(replace);
            Assert.IsTrue(result2.Success);

            //check that doc has been updated
            var get = new Get<dynamic>(key, GetVBucket(),  Converter, Transcoder);
            var result3 = IOStrategy.Execute(get);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(result3.Value.bar.Value, "bar");
        }

        [Test]
        public void When_Document_Does_Not_Exist_Replace_Fails()
        {
            const string key = "Replace.When_Document_Does_Not_Exist_Replace_Fails";

            //delete the value if it exists
            var delete = new Delete(key, GetVBucket(), Converter, Transcoder);
            var result = IOStrategy.Execute(delete);
            Console.WriteLine(result.Message);

            //replace it the old doc with a new one
            var replace = new Replace<dynamic>(key, new { bar = "bar" }, GetVBucket(), Converter, Transcoder);
            var result2 = IOStrategy.Execute(replace);
            Assert.IsFalse(result2.Success);
            Assert.AreEqual(ResponseStatus.KeyNotFound, result2.Status);
        }

        [Test]
        public void Test_Clone()
        {
            var operation = new Replace<string>("key", "somevalue", GetVBucket(), Converter, Transcoder)
            {
                Cas = 1123
            };
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
