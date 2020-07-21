using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Couchbase.Configuration.Client;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.Errors;
using Couchbase.IO.Services;
using Couchbase.IO.Utils;
using Couchbase.UnitTests.IO.Operations;
using Moq;
using NUnit.Framework;
using OpenTracing.Noop;

namespace Couchbase.UnitTests.IO.Services
{
    [TestFixture]
    public class PooledIOServiceTests
    {
        [Test]
        public void When_EnhanchedDurability_Is_True_Hello_Requests_MutationSeqNo()
        {
            const ulong connectionId = 12345;
            var mockConnection = new Mock<IConnection>();
            mockConnection.Setup(x => x.ConnectionId).Returns(connectionId);
            mockConnection.Setup(x => x.MustEnableServerFeatures).Returns(true);

            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);
            mockConnectionPool.SetupGet(x => x.Configuration).Returns(new PoolConfiguration
            {
                UseEnhancedDurability = true,
                ClientConfiguration = new ClientConfiguration {Tracer = NoopTracerFactory.Create() }
            });
            mockConnectionPool.Setup(x => x.Connections).Returns(new List<IConnection> { mockConnection.Object });

            var service = new PooledIOService(mockConnectionPool.Object);

            service.Execute(new FakeOperationWithRequiredKey("key", null, new DefaultTranscoder(), 0));

            var features = new[]
            {
                (short) ServerFeatures.SubdocXAttributes,
                (short) ServerFeatures.SelectBucket,
                (short) ServerFeatures.XError,
                (short) ServerFeatures.MutationSeqno,
                (short) ServerFeatures.ServerDuration
            };
            var key = IOServiceBase.BuildHelloKey(connectionId);
            var expectedBytes = new Hello(key, features.ToArray(), new DefaultTranscoder(), 0, 0).Write();

            mockConnectionPool.Verify(x => x.Acquire(), Times.Once);
            mockConnection.Verify(x => x.Send(It.Is<byte[]>(bytes => bytes.SequenceEqual(expectedBytes))));
        }

        [Test]
        public void When_EnhanchedDurability_Is_False_Hello_Doesnt_Requests_MutationSeqNo()
        {
            const ulong connectionId = 12345;
            var mockConnection = new Mock<IConnection>();
            mockConnection.Setup(x => x.ConnectionId).Returns(connectionId);
            mockConnection.Setup(x => x.MustEnableServerFeatures).Returns(true);

            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);
            mockConnectionPool.SetupGet(x => x.Configuration).Returns(new PoolConfiguration
            {
                UseEnhancedDurability = false,
                ClientConfiguration = new ClientConfiguration {Tracer = NoopTracerFactory.Create() }
            });
            mockConnectionPool.Setup(x => x.Connections).Returns(new List<IConnection> { mockConnection.Object });

            var service = new PooledIOService(mockConnectionPool.Object);

            service.Execute(new FakeOperationWithRequiredKey("key", null, new DefaultTranscoder(), 0));

            var features = new[]
            {
                (short) ServerFeatures.SubdocXAttributes,
                (short) ServerFeatures.SelectBucket,
                (short) ServerFeatures.XError,
                (short) ServerFeatures.ServerDuration
            };
            var key = IOServiceBase.BuildHelloKey(connectionId);
            var expectedBytes = new Hello(key, features.ToArray(), new DefaultTranscoder(), 0, 0).Write();

            mockConnectionPool.Verify(x => x.Acquire(), Times.Once);
            mockConnection.Verify(x => x.Send(It.Is<byte[]>(bytes => bytes.SequenceEqual(expectedBytes))));
        }

        [Test]
        public void Result_Has_KVError_In_Message_If_ErrorMap_Available_And_Failure()
        {
            const string codeString = "2c"; // 44
            var code = short.Parse(codeString, NumberStyles.HexNumber);
            var errorCode = new ErrorCode { Name = "test" };
            var errorMap = new ErrorMap
            {
                Version = 1,
                Revision = 1,
                Errors = new Dictionary<string, ErrorCode>
                {
                    {codeString, errorCode}
                }
            };

            var converter = new DefaultConverter();
            var responseBytes = new byte[24];
            converter.FromByte((byte)Magic.Response, responseBytes, HeaderIndexFor.Magic);
            converter.FromInt16(code, responseBytes, HeaderIndexFor.Status);

            var mockConnection = new Mock<IConnection>();
            mockConnection.Setup(x => x.IsConnected).Returns(true);
            mockConnection.Setup(x => x.Send(It.IsAny<byte[]>())).Returns(responseBytes);

            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);
            mockConnectionPool.SetupGet(x => x.Configuration).Returns(new PoolConfiguration
            {
                ClientConfiguration = new ClientConfiguration {Tracer = NoopTracerFactory.Create() }
            });
            mockConnectionPool.Setup(x => x.Connections).Returns(new List<IConnection> { mockConnection.Object });

            var service = new PooledIOService(mockConnectionPool.Object)
            {
                ErrorMap = errorMap
            };

            var result = service.Execute(new FakeOperationWithRequiredKey("key", null, new DefaultTranscoder(), 0, 0));

            Assert.True(result.Message.Contains("KV Error"));
        }

        [Test]
        public void Result_Has_UnknownError_Status_If_ErrorMap_Not_Available()
        {
            const string codeString = "2c"; // 44
            var code = short.Parse(codeString, NumberStyles.HexNumber);
            var errorCode = new ErrorCode { Name = "test" };
            var errorMap = new ErrorMap
            {
                Version = 1,
                Revision = 1,
                Errors = new Dictionary<string, ErrorCode>()
            };

            var converter = new DefaultConverter();
            var responseBytes = new byte[24];
            converter.FromByte((byte)Magic.Response, responseBytes, HeaderIndexFor.Magic);
            converter.FromInt16(code, responseBytes, HeaderIndexFor.Status);

            var mockConnection = new Mock<IConnection>();
            mockConnection.Setup(x => x.IsConnected).Returns(true);
            mockConnection.Setup(x => x.Send(It.IsAny<byte[]>())).Returns(responseBytes);

            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);
            mockConnectionPool.SetupGet(x => x.Configuration).Returns(new PoolConfiguration
            {
                ClientConfiguration = new ClientConfiguration {Tracer = NoopTracerFactory.Create() }
            });
            mockConnectionPool.Setup(x => x.Connections).Returns(new List<IConnection> {mockConnection.Object});

            var service = new PooledIOService(mockConnectionPool.Object)
            {
                ErrorMap = errorMap
            };

            var result = service.Execute(new FakeOperationWithRequiredKey("key", null, new DefaultTranscoder(), 0, 0));

            Assert.AreEqual(ResponseStatus.UnknownError, result.Status);
            Assert.AreEqual("Status code: UnknownError [-2]", result.Message);
        }

        [Test]
        public void Connection_acquired_during_construction_should_be_released_back_to_pool()
        {
            var inUse = false;
            var connection = new Mock<IConnection>();

            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool
                .Setup(x => x.Acquire())
                .Returns(() =>
                {
                    inUse = true;
                    return connection.Object;
                }
            );
            mockConnectionPool
                .Setup(x => x.Release(It.IsAny<IConnection>()))
                .Callback<IConnection>(conn =>
                {
                    inUse = false;
                });

            mockConnectionPool.Setup(x => x.Connections).Returns(() => { return new [] {connection.Object}; });

            // create io service
            var ioService = new PooledIOService(mockConnectionPool.Object);

            // connection is marked as in use during constructor, after being released it should be false
            Assert.IsFalse(inUse);
        }
    }
}
