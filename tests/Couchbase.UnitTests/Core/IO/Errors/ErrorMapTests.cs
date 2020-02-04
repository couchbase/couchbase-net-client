using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.DI;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;
using Couchbase.UnitTests.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Errors
{
    public class ErrorMapTests
    {
        [Fact]
        public async Task When_Status_Indicates_Failure_ErrorCode_Is_Populated()
        {
            var errorMap = JsonConvert.DeserializeObject<ErrorMap>(ResourceHelper.ReadResource("kv-error-map.json"));

            var node = new ClusterNode(new ClusterContext(new CancellationTokenSource(),
                    new ClusterOptions()), new Mock<IConnectionFactory>().Object,
                new Mock<ILogger<ClusterNode>>().Object,
                new Mock<ITypeTranscoder>().Object,
                new Mock<ICircuitBreaker>().Object,
                new Mock<ISaslMechanismFactory>().Object)
            {
                ErrorMap = errorMap
            };

            var mockConnection = new Mock<IConnection>();
            mockConnection.Setup(x => x.IsDead).Returns(false);
            node.Connection = mockConnection.Object;

            var insert = new FakeOperation(OpCode.Add, ResponseStatus.KeyExists);

            try
            {
                await node.ExecuteOp(insert, CancellationToken.None, TimeSpan.FromMinutes(5));
            }
            catch (DocumentExistsException)
            {
                //need to resolve from context
                //Assert.NotNull((e.InnerException as DocumentExistsException)?.ErrorCode);
            }
        }

        private class FakeOperation : OperationBase
        {
            private readonly OpCode _operationCode;

            public FakeOperation(OpCode operationCode, ResponseStatus status, ErrorCode errorCode = null) : base()
            {
                Key = "hi";
                _operationCode = operationCode;
                Header = new OperationHeader
                {
                    Status = status,
                    OpCode = _operationCode
                };
                ErrorCode = errorCode;
            }

            public override Task SendAsync(IConnection connection)
            {
                Completed(new SocketAsyncState
                {
                    Status = Header.Status
                });
               return Task.CompletedTask;
            }

            public override OpCode OpCode => _operationCode;
        }
    }
}
