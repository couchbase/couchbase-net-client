using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.DI;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.KeyValue;
using Couchbase.Management.Buckets;
using Couchbase.UnitTests.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Errors
{
    public class ErrorMapTests
    {
        [Fact]
        public async Task When_Status_Indicates_Failure_Context_Is_Populated()
        {
            var errorMap = new ErrorMap(JsonConvert.DeserializeObject<ErrorMapDto>(ResourceHelper.ReadResource("kv-error-map.json")));

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
                    new ClusterOptions()), mockConnectionPoolFactory.Object,
                new Mock<ILogger<ClusterNode>>().Object,
                new DefaultObjectPool<OperationBuilder>(new OperationBuilderPoolPolicy()),
                new Mock<ICircuitBreaker>().Object,
                new Mock<ISaslMechanismFactory>().Object,
                new Mock<IRedactor>().Object,
                new IPEndPoint(IPAddress.Parse("127.0.0.1"), 11210),
                BucketType.Couchbase,
                new NodeAdapter
                {
                    Hostname = "127.0.0.1"
                },
                NoopRequestTracer.Instance,
                NoopValueRecorder.Instance)
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
                await node.ExecuteOp(insert).ConfigureAwait(false);
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
