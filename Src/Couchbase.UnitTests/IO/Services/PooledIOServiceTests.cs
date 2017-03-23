using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Couchbase.Configuration.Client;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Services;
using Couchbase.IO.Utils;
using Couchbase.UnitTests.IO.Operations;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Services
{
    [TestFixture]
    public class PooledIOServiceTests
    {
        [Test]
        public void When_EnhanchedDurability_Is_True_Hello_Requests_MutationSeqNo()
        {
            var mockConnection = new Mock<IConnection>();
            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);
            mockConnectionPool.SetupGet(x => x.Configuration).Returns(new PoolConfiguration { UseEnhancedDurability = true });

            var service = new PooledIOService(mockConnectionPool.Object);

            service.Execute(new FakeOperationWithRequiredKey("key", null, new DefaultTranscoder(), 0));

            var features = new short[] {(byte) ServerFeatures.SubdocXAttributes, (byte) ServerFeatures.SelectBucket, (byte) ServerFeatures.MutationSeqno};
            var expectedBytes = new Hello(features.ToArray(), new DefaultTranscoder(), 0, 0).Write();

            mockConnectionPool.Verify(x => x.Acquire(), Times.Once);
            mockConnection.Verify(x => x.Send(It.Is<byte[]>(bytes => bytes.SequenceEqual(expectedBytes))));
        }

        [Test]
        public void When_EnhanchedDurability_Is_False_Hello_Doesnt_Requests_MutationSeqNo()
        {
            var mockConnection = new Mock<IConnection>();
            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);
            mockConnectionPool.SetupGet(x => x.Configuration).Returns(new PoolConfiguration { UseEnhancedDurability = false });

            var service = new PooledIOService(mockConnectionPool.Object);

            service.Execute(new FakeOperationWithRequiredKey("key", null, new DefaultTranscoder(), 0));

            var features = new short[] {(byte) ServerFeatures.SubdocXAttributes, (byte) ServerFeatures.SelectBucket};
            var expectedBytes = new Hello(features.ToArray(), new DefaultTranscoder(), 0, 0).Write();

            mockConnectionPool.Verify(x => x.Acquire(), Times.Once);
            mockConnection.Verify(x => x.Send(It.Is<byte[]>(bytes => bytes.SequenceEqual(expectedBytes))));
        }

        [Test]
        public void Result_Has_Failure_Status_If_ErrorMap_Available()
        {
            const string codeString = "2C"; // 44
            var code = short.Parse(codeString, NumberStyles.HexNumber);
            var errorCode = new ErrorCode { name = "test" };
            var errorMap = new ErrorMap
            {
                version = 1,
                revision = 1,
                errors = new Dictionary<string, ErrorCode>
                {
                    {codeString, errorCode}
                }
            };

            var converter = new DefaultConverter();
            var responseBytes = new byte[24];
            converter.FromInt16(code, responseBytes, HeaderIndexFor.Status);

            var mockConnection = new Mock<IConnection>();
            mockConnection.Setup(x => x.IsConnected).Returns(true);
            mockConnection.Setup(x => x.Send(It.IsAny<byte[]>())).Returns(responseBytes);

            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);
            mockConnectionPool.SetupGet(x => x.Configuration).Returns(new PoolConfiguration());

            var service = new PooledIOService(mockConnectionPool.Object);
            service.SetErrorMap(errorMap);

            var result = service.Execute(new FakeOperationWithRequiredKey("key", null, new DefaultTranscoder(), 0, 0));

            Assert.AreEqual(ResponseStatus.Failure, result.Status);
            Assert.AreEqual(errorCode.ToString(), result.Message);
        }

        [Test]
        public void Result_Has_UnknownError_Status_If_ErrorMap_Not_Available()
        {
            const string codeString = "2C"; // 44
            var code = short.Parse(codeString, NumberStyles.HexNumber);
            var errorCode = new ErrorCode { name = "test" };
            var errorMap = new ErrorMap
            {
                version = 1,
                revision = 1,
                errors = new Dictionary<string, ErrorCode>()
            };

            var converter = new DefaultConverter();
            var responseBytes = new byte[24];
            converter.FromInt16(code, responseBytes, HeaderIndexFor.Status);

            var mockConnection = new Mock<IConnection>();
            mockConnection.Setup(x => x.IsConnected).Returns(true);
            mockConnection.Setup(x => x.Send(It.IsAny<byte[]>())).Returns(responseBytes);

            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);
            mockConnectionPool.SetupGet(x => x.Configuration).Returns(new PoolConfiguration());

            var service = new PooledIOService(mockConnectionPool.Object);
            service.SetErrorMap(errorMap);

            var result = service.Execute(new FakeOperationWithRequiredKey("key", null, new DefaultTranscoder(), 0, 0));

            Assert.AreEqual(ResponseStatus.UnknownError, result.Status);
            Assert.AreEqual(converter.ToString(responseBytes, 0, 24), result.Message);
        }
    }
}
