using System;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.SubDocument;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations.Subdocument
{
    [TestFixture]
    public class SubDocSingularBaseTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Test_Empty_Key_Throws_KeyException(string key)
        {
            Assert.Throws<MissingKeyException>(() => new FakeSubDocumentOperation<dynamic>(null, key, null, null, 0), "Key cannot be empty.");
        }
    }

    internal class FakeSubDocumentOperation<T> : SubDocSingularBase<T>
    {
        public FakeSubDocumentOperation(ISubDocBuilder<T> builder, string key, IVBucket vBucket, ITypeTranscoder transcoder, uint opaque, uint timeout)
            : base(builder, key, vBucket, transcoder, opaque, timeout)
        {
        }

        public FakeSubDocumentOperation(ISubDocBuilder<T> builder, string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(builder, key, vBucket, transcoder, timeout)
        {
        }

        public override OperationCode OperationCode
        {
            get { throw new NotImplementedException(); }
        }
    }
}
