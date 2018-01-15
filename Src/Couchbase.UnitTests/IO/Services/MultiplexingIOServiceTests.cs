using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.Core.Buckets;
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
using OpenTracing.NullTracer;

namespace Couchbase.UnitTests.IO.Services
{
    [TestFixture]
    public class MultiplexingIOServiceTests
    {
        [Test]
        public async Task When_TransportFailure_Occurs_CheckConfigUpdate_Is_Called_For_ExecuteAsync()
        {
            var mockConnection = new Mock<IConnection>();
            mockConnection.Setup(x => x.IsConnected).Returns(false);

            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);
            mockConnectionPool.SetupGet(x => x.Configuration).Returns(new PoolConfiguration { UseEnhancedDurability = false });

            var service = new MultiplexingIOService(mockConnectionPool.Object);

            var mockOperation = new Mock<IOperation>();
            mockOperation.Setup(x => x.Opaque).Returns(1);
            mockOperation.Setup(x => x.Exception).Returns(new TransportFailureException());

            var opqueue = new ConcurrentDictionary<uint, IOperation>();
            opqueue.TryAdd(mockOperation.Object.Opaque, mockOperation.Object);

            var mockController = new Mock<IClusterController>();
            mockController.Setup(x=>x.CheckConfigUpdate(It.IsAny<string>(), It.IsAny<IPEndPoint>())).Verifiable();

            TaskCompletionSource<IOperationResult> tcs = new TaskCompletionSource<IOperationResult>();
            mockOperation.Setup(x => x.Completed)
                .Returns(CallbackFactory.CompletedFuncWithRetryForCouchbase(null, opqueue, mockController.Object, tcs,
                    new CancellationToken()));

            await service.ExecuteAsync(mockOperation.Object);
            mockController.VerifyAll();
        }

        [Test]
        public async Task When_TransportFailure_Occurs_CheckConfigUpdate_Is_Called_For_ExecuteAsync_T()
        {
            var mockConnection = new Mock<IConnection>();
            mockConnection.Setup(x => x.IsConnected).Returns(false);

            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);
            mockConnectionPool.SetupGet(x => x.Configuration).Returns(new PoolConfiguration { UseEnhancedDurability = false });

            var service = new MultiplexingIOService(mockConnectionPool.Object);

            var mockOperation = new Mock<IOperation<string>>();
            mockOperation.Setup(x => x.Opaque).Returns(1);
            mockOperation.Setup(x => x.Exception).Returns(new TransportFailureException());

            var opqueue = new ConcurrentDictionary<uint, IOperation>();
            opqueue.TryAdd(mockOperation.Object.Opaque, mockOperation.Object);

            var mockController = new Mock<IClusterController>();
            mockController.Setup(x => x.CheckConfigUpdate(It.IsAny<string>(), It.IsAny<IPEndPoint>())).Verifiable();

            TaskCompletionSource<IOperationResult<string>> tcs = new TaskCompletionSource<IOperationResult<string>>();
            mockOperation.Setup(x => x.Completed)
                .Returns(CallbackFactory.CompletedFuncWithRetryForCouchbase<string>(null, opqueue, mockController.Object, tcs,
                    new CancellationToken()));

            await service.ExecuteAsync(mockOperation.Object);
            mockController.VerifyAll();
        }

        [Test]
        public void When_EnhanchedDurability_Is_True_Hello_Requests_MutationSeqNo()
        {
            const ulong connectionId = 12345;
            var mockConnection = new Mock<IConnection>();
            mockConnection.Setup(x => x.ConnectionId).Returns(connectionId);
            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);
            mockConnectionPool.SetupGet(x => x.Configuration).Returns(new PoolConfiguration
            {
                UseEnhancedDurability = true,
                ClientConfiguration = new ClientConfiguration { Tracer = NullTracer.Instance }
            });

            var service = new MultiplexingIOService(mockConnectionPool.Object);

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
            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);
            mockConnectionPool.SetupGet(x => x.Configuration).Returns(new PoolConfiguration
            {
                UseEnhancedDurability = false,
                ClientConfiguration = new ClientConfiguration {Tracer = NullTracer.Instance}
            });

            var service = new MultiplexingIOService(mockConnectionPool.Object);

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
        public void When_NotConnected_Execute_ReturnsTransportFailureException()
        {
            var mockConnection = new Mock<IConnection>();
            mockConnection.Setup(x => x.IsConnected).Returns(false);

            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);
            mockConnectionPool.SetupGet(x => x.Configuration).Returns(new PoolConfiguration
            {
                UseEnhancedDurability = false,
                ClientConfiguration = new ClientConfiguration { Tracer = NullTracer.Instance }
            });

            var service = new MultiplexingIOService(mockConnectionPool.Object);
            var result = service.Execute(new FakeOperationWithRequiredKey("key", null, new DefaultTranscoder(), 0));

            Assert.AreEqual(result.Status, ResponseStatus.TransportFailure);
            Assert.IsFalse(result.Success);
            Assert.IsInstanceOf<TransportFailureException>(result.Exception);
        }

        [Test]
        public async Task When_NotConnected_ExecuteAsync_ReturnsTransportFailureException()
        {
            var mockConnection = new Mock<IConnection>();
            mockConnection.Setup(x => x.IsConnected).Returns(false);

            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);
            mockConnectionPool.SetupGet(x => x.Configuration).Returns(new PoolConfiguration { UseEnhancedDurability = false });

            var service = new MultiplexingIOService(mockConnectionPool.Object);
            await service.ExecuteAsync(new FakeOperationWithRequiredKey("key", null, new DefaultTranscoder(), 0)
            {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
                Completed = async s =>
                {
                    Assert.AreEqual(s.Status, ResponseStatus.TransportFailure);
                    Assert.IsInstanceOf<TransportFailureException>(s.Exception);
                }
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
            });
        }

        [Test]
        public void Result_Has_Failure_Status_If_ErrorMap_Available()
        {
            const string codeString = "2c"; // 44
            var code = short.Parse(codeString, NumberStyles.HexNumber);
            var errorCode = new ErrorCode {Name = "test"};
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
            converter.FromByte((byte) Magic.Response, responseBytes, HeaderIndexFor.Magic);
            converter.FromInt16(code, responseBytes, HeaderIndexFor.Status);

            var mockConnection = new Mock<IConnection>();
            mockConnection.Setup(x => x.IsConnected).Returns(true);
            mockConnection.Setup(x => x.Send(It.IsAny<byte[]>())).Returns(responseBytes);

            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool.Setup(x => x.Acquire()).Returns(mockConnection.Object);
            mockConnectionPool.SetupGet(x => x.Configuration).Returns(new PoolConfiguration
            {
                ClientConfiguration = new ClientConfiguration { Tracer = NullTracer.Instance }
            });

            var service = new MultiplexingIOService(mockConnectionPool.Object)
            {
                ErrorMap = errorMap
            };

            var result = service.Execute(new FakeOperationWithRequiredKey("key", null, new DefaultTranscoder(), 0, 0));

            Assert.AreEqual(ResponseStatus.Failure, result.Status);
            Assert.AreEqual(errorCode.ToString(), result.Message);
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
                ClientConfiguration = new ClientConfiguration { Tracer = NullTracer.Instance }
            });

            var service = new MultiplexingIOService(mockConnectionPool.Object)
            {
                ErrorMap = errorMap
            };

            var result = service.Execute(new FakeOperationWithRequiredKey("key", null, new DefaultTranscoder(), 0, 0));

            Assert.AreEqual(ResponseStatus.UnknownError, result.Status);
            Assert.AreEqual("Status code: UnknownError [-2]", result.Message);
        }
    }
}
