using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations
{
    [TestFixture]
    public class OperationBaseTests
    {
        [Test]
        public void GetConfig_Without_Transcoder_Parameter_Uses_Content_Transcoder()
        {
            var config = new BucketConfig();
            var contentTranscoder = new Mock<ITypeTranscoder>();
            contentTranscoder
                .Setup(transcoder => transcoder.Decode<BucketConfig>(It.IsAny<byte[]>(), It.IsAny<int>(),
                    It.IsAny<int>(), It.IsAny<Flags>(), It.IsAny<OperationCode>()))
                .Returns(config);
            var operation = new FakeOperation(contentTranscoder.Object);

            var result = operation.GetConfig();
            Assert.AreSame(config, result);

            // make sure the transcoder for decoding document content is not used
            contentTranscoder.Verify(
                t => t.Decode<BucketConfig>(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Flags>(),
                    It.IsAny<OperationCode>()), Times.Once);
        }

        [Test]
        public void Getconfig_Uses_Paramater_Transcoder()
        {
            var contentTranscoder = new Mock<ITypeTranscoder>();
            var operation = new FakeOperation(contentTranscoder.Object);

            var config = new BucketConfig();
            var configTranscoder = new Mock<ITypeTranscoder>();
            configTranscoder
                .Setup(transcoder => transcoder.Decode<BucketConfig>(It.IsAny<byte[]>(), It.IsAny<int>(),
                    It.IsAny<int>(), It.IsAny<Flags>(), It.IsAny<OperationCode>()))
                .Returns(config);

            var result = operation.GetConfig(configTranscoder.Object);
            Assert.AreSame(config, result);

            // make sure the transcoder for decoding document content is not used
            configTranscoder.Verify(
                t => t.Decode<BucketConfig>(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Flags>(),
                    It.IsAny<OperationCode>()), Times.Once);
            contentTranscoder.Verify(
                t => t.Decode<BucketConfig>(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<Flags>(),
                    It.IsAny<OperationCode>()), Times.Never);
        }

        private class FakeOperation : OperationBase
        {
            public FakeOperation(ITypeTranscoder transcoder)
                : base("hello", null, transcoder, 0)
            {
            }

            public override OperationCode OperationCode
            {
                get { return OperationCode.Get; }
            }

            public override ResponseStatus GetResponseStatus()
            {
                return ResponseStatus.VBucketBelongsToAnotherServer;
            }
        }
    }
}
