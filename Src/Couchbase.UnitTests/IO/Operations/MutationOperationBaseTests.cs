using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations
{
    [TestFixture]
    public class MutationOperationBaseTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Test_Empty_Key_Throws_KeyException(string key)

        {
            Assert.Throws<MissingKeyException>(() => new FakeOperationWithRequiredKey(key, null, null, 0));
        }
    }

    internal class FakeOperationWithRequiredKey : MutationOperationBase
    {
        public override OperationCode OperationCode
        {
            get { return OperationCode.Add; }
        }

        public FakeOperationWithRequiredKey(string key, IVBucket vBucket, ITypeTranscoder transcoder, uint opaque, uint timeout)
            : base(key, vBucket, transcoder, opaque, timeout)
        {
        }

        public FakeOperationWithRequiredKey(string key, IVBucket vBucket, ITypeTranscoder transcoder, uint timeout)
            : base(key, vBucket, transcoder, timeout)
        {
        }

        public override bool RequiresKey
        {
            get { return true; }
        }
    }
}
