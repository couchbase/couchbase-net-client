using Couchbase.Core.Transcoders;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations
{
    [TestFixture]
    public class GetLTests
    {
        [Test]
        public void Sets_Expiration_Value_In_Extras()
        {
            const uint expiration = 10;
            var transcoder = new DefaultTranscoder();
            var operation = new GetL<dynamic>("key", null, transcoder, 0)
            {
                Expiration = expiration
            };

            var bytes = operation.Write();
            Assert.AreEqual(expiration, transcoder.Converter.ToUInt32(bytes, 24));
        }
    }
}
