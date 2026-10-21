using System;
using System.Buffers;
using System.Threading.Tasks;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.IO
{
    // NCBC-4328 (B): an exception while completing an operation was rethrown from a thread-pool callback, which
    // ends the process. The callback is invoked directly here, on the test thread, so a regression fails the test
    // instead of taking down the test host.
    public class AsyncStateBaseTests
    {
        [Fact]
        public void SendResponseInternal_HandlerThrows_FailsTheOperationInsteadOfRethrowing()
        {
            // Arrange

            var failure = new InvalidOperationException("handler failed");
            var operation = new Mock<IOperation>();
            operation.Setup(m => m.HandleOperationCompleted(in It.Ref<SlicedMemoryOwner<byte>>.IsAny)).Throws(failure);
            operation.Setup(m => m.TrySetException(It.IsAny<Exception>())).Returns(true);

            var state = new TestState(operation.Object)
            {
                _response = MemoryPool<byte>.Shared.RentAndSlice(24),
            };

            // Act

            var thrown = Record.Exception(() => TestState.RunCallback(state));

            // Assert

            Assert.Null(thrown);
            operation.Verify(m => m.TrySetException(failure), Times.Once);
        }

        [Fact]
        public async Task HandleOperationCompleted_FreedBuffer_FaultsTheOperation()
        {
            // Arrange

            // The response buffer was freed before the operation read it: the read throws.
            var response = MemoryPool<byte>.Shared.RentAndSlice(24);
            response.Dispose();
            var operation = new Noop();

            // Act

            var thrown = Record.Exception(() => operation.HandleOperationCompleted(in response));

            // Assert

            Assert.Null(thrown);
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await operation.Completed);
        }

        private sealed class TestState : AsyncStateBase
        {
            public TestState(IOperation operation) : base(operation)
            {
            }

            public static void RunCallback(AsyncStateBase state) => SendResponseInternal(state);
        }
    }
}
