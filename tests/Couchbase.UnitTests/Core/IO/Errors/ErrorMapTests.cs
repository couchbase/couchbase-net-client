using System;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.DI;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;
using Couchbase.Core.Logging;
using Couchbase.Management.Buckets;
using Couchbase.UnitTests.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Errors
{
    public class ErrorMapTests
    {
        [Fact]
        public async Task When_Status_Indicates_Failure_Context_Is_Populated()
        {
            var errorMap = new ErrorMap(JsonSerializer.Deserialize(ResourceHelper.ReadResource("kv-error-map.json"),
                InternalSerializationContext.Default.ErrorMapDto)!);

            var mockConnection = new Mock<IConnection>();

            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool
                .Setup(m => m.SendAsync(It.IsAny<IOperation>(), It.IsAny<CancellationToken>()))
                .Returns((IOperation operation, CancellationToken _) => operation.SendAsync(mockConnection.Object));

            var mockConnectionPoolFactory = new Mock<IConnectionPoolFactory>();
            mockConnectionPoolFactory
                .Setup(m => m.Create(It.IsAny<ClusterNode>()))
                .Returns(mockConnectionPool.Object);

            var node = new ClusterNode(new ClusterContext(new CancellationTokenSource(),
                    new ClusterOptions().WithPasswordAuthentication("username", "password")), mockConnectionPoolFactory.Object,
                new Mock<ILogger<ClusterNode>>().Object,
                new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                new Mock<ICircuitBreaker>().Object,
                new Mock<ISaslMechanismFactory>().Object,
                new TypedRedactor(RedactionLevel.None),
                new HostEndpointWithPort("127.0.0.1", 11210),
                new NodeAdapter
                {
                    Hostname = "127.0.0.1"
                },
                NoopRequestTracer.Instance,
                new Mock<IOperationConfigurator>().Object)
            {
                ErrorMap = errorMap
            };

            var insert = new FakeOperation(OpCode.Add, ResponseStatus.KeyExists)
            {
                SName = "TheScope",
                CName = "TheCollection"
            };

            try
            {
                await node.SendAsync(insert);
            }
            catch (DocumentExistsException e)
            {
                var context = e.Context as KeyValueErrorContext;
                Assert.NotNull(e.Context);
                var message =
                    "KV Error: {Name=\"KEY_EEXISTS\", Description=\"key already exists, or CAS mismatch\", Attributes=\"item-only\"}";
                Assert.Equal(message, e.Context.Message);
                Assert.Equal("TheScope", context.ScopeName);
                Assert.Equal("TheCollection", context.CollectionName);
                Assert.NotEqual("0", context.ClientContextId);
            }
        }

        [Theory]
        [InlineData(ResponseStatus.RateLimitedMaxCommands, "RATE_LIMITED_MAX_COMMANDS")]
        [InlineData(ResponseStatus.RateLimitedMaxConnections, "RATE_LIMITED_MAX_CONNECTIONS")]
        [InlineData(ResponseStatus.RateLimitedNetworkEgress, "RATE_LIMITED_NETWORK_EGRESS")]
        [InlineData(ResponseStatus.RateLimitedNetworkIngress, "RATE_LIMITED_NETWORK_INGRESS")]
        public async Task Test_ClusterMap_Version2(ResponseStatus status, string errorCode)
        {
            var errorMap = new ErrorMap(JsonSerializer.Deserialize(ResourceHelper.ReadResource("kv-error-map-v2.json"),
                InternalSerializationContext.Default.ErrorMapDto)!);

            var mockConnection = new Mock<IConnection>();

            var mockConnectionPool = new Mock<IConnectionPool>();
            mockConnectionPool
                .Setup(m => m.SendAsync(It.IsAny<IOperation>(), It.IsAny<CancellationToken>()))
                .Returns((IOperation operation, CancellationToken _) => operation.SendAsync(mockConnection.Object));

            var mockConnectionPoolFactory = new Mock<IConnectionPoolFactory>();
            mockConnectionPoolFactory
                .Setup(m => m.Create(It.IsAny<ClusterNode>()))
                .Returns(mockConnectionPool.Object);

            var node = new ClusterNode(new ClusterContext(new CancellationTokenSource(),
                    new ClusterOptions().WithPasswordAuthentication("username", "password")), mockConnectionPoolFactory.Object,
                new Mock<ILogger<ClusterNode>>().Object,
                new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                new Mock<ICircuitBreaker>().Object,
                new Mock<ISaslMechanismFactory>().Object,
                new TypedRedactor(RedactionLevel.None),
                new HostEndpointWithPort("127.0.0.1", 11210),
                new NodeAdapter
                {
                    Hostname = "127.0.0.1"
                },
                NoopRequestTracer.Instance,
                new Mock<IOperationConfigurator>().Object)
            {
                ErrorMap = errorMap
            };

            var insert = new FakeOperation(OpCode.Add, status)
            {
                SName = "TheScope",
                CName = "TheCollection"
            };

            try
            {
                await node.SendAsync(insert);
            }
            catch (CouchbaseException e)
            {
                var context = e.Context as KeyValueErrorContext;
                Assert.NotNull(e.Context);

                Assert.Contains(errorCode, e.Context.Message);
                Assert.Equal("TheScope", context.ScopeName);
                Assert.Equal("TheCollection", context.CollectionName);
                Assert.NotEqual("0", context.ClientContextId);
            }
        }

        private class FakeOperation : OperationBase
        {
            private readonly OpCode _operationCode;

            public FakeOperation(OpCode operationCode, ResponseStatus status) : base()
            {
                Key = "hi";
                _operationCode = operationCode;
                Header = new OperationHeader
                {
                    Status = status,
                    OpCode = _operationCode
                };
            }

            public override Task SendAsync(IConnection connection, CancellationToken cancellationToken = default)
            {
                HandleOperationCompleted(AsyncState.BuildErrorResponse(0, Header.Status));
                return Task.CompletedTask;
            }

            public override OpCode OpCode => _operationCode;
        }
    }
}
