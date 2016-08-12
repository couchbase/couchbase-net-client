using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations.SubDocument;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations.Subdocument
{
    public class MultiMutationTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Test_Empty_Key_Throws_KeyException(string key)

        {
            Assert.Throws<MissingKeyException>(() => new FakeMultiMutationOperation<dynamic>(key, null, null, null, 0), "Key cannot be empty.");
        }
    }

    internal class FakeMultiMutationOperation<T> : MultiMutation<T>
    {
        public FakeMultiMutationOperation(string key, MutateInBuilder<T> mutateInBuilder, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, mutateInBuilder, vBucket, transcoder, timeout)
        {
        }
    }
}
