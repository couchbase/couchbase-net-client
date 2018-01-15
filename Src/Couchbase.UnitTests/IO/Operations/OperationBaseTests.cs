using System;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.Errors;
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

        [Test]
        public void When_ErrorMap_Is_Null_ErrorMapRequestsRetry_Is_False()
        {
            var op = new FakeOperation(new DefaultTranscoder());
            Assert.False(op.ErrorMapRequestsRetry());
        }

        [Test]
        public void When_ErrorMap_Is_Not_Null_And_RetryStrategy_Is_None_ErrorMapRequestsRetry_Is_False()
        {
            var op = new FakeOperation(new ErrorCode{ Retry = new RetrySpec{ Strategy = RetryStrategy.None}});

            Assert.False(op.ErrorMapRequestsRetry());
        }

        [Test]
        public void When_()
        {
            var op = new FakeOperation(new ErrorCode { Retry = new RetrySpec { Strategy = RetryStrategy.None } });
        }

        [Test]
        [TestCase(RetryStrategy.Linear)]
        [TestCase(RetryStrategy.Constant)]
        [TestCase(RetryStrategy.Exponential)]
        public void When_ErrorMap_Is_Not_Null_And_RetryStrategy_Is_Not_None_ErrorMapRequestsRetry_Is_True(RetryStrategy strategy)
        {
            var op = new FakeOperation(new ErrorCode { Retry = new RetrySpec { Strategy = strategy} });

            Assert.True(op.ErrorMapRequestsRetry());
        }

        [TestCase((ushort) 0, 0.0)]
        [TestCase((ushort) 1234, 119635.03533802561)]
        [TestCase((ushort) 65535, 120125042.10125735)]
        public void Can_Decode_Server_Duration(ushort encoded, double expected)
        {
            var decoded = Math.Pow(encoded, 1.74) / 2;
            Assert.AreEqual(expected, decoded);
        }

        private class FakeOperation : OperationBase
        {
            public FakeOperation(ITypeTranscoder transcoder)
                : base("hello", null, transcoder, 0)
            {
            }

            public FakeOperation(ErrorCode errorCode)
                : this(new DefaultTranscoder())
            {
                ErrorCode = errorCode;
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
