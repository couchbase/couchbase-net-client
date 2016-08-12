using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations.SubDocument;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations.Subdocument
{
    [TestFixture]
    public class MultiGetTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Test_Empty_Key_Throws_KeyException(string key)
        {
            Assert.Throws<MissingKeyException>(() => new FakeMultiLookupOperation<dynamic>(key, null, null, null, 0), "Key cannot be empty.");
        }
    }

    internal class FakeMultiLookupOperation<T> : MultiLookup<T>
    {
        public FakeMultiLookupOperation(string key, LookupInBuilder<T> builder, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, builder, vBucket, transcoder, timeout)
        {
        }
    }
}
