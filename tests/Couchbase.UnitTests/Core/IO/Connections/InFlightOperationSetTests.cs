using System;
using System.Buffers;
using System.Threading.Tasks;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Connections
{
    public class InFlightOperationSetTests
    {
        #region Add

        [Fact]
        public void Add_NewState_AddsToCount()
        {
            // Arrange

            var state = MakeState(5);

            using var set = new InFlightOperationSet();

            // Act

            set.Add(state, 75000);

            // Assert

            Assert.Equal(1, set.Count);
        }

        [Fact]
        public async Task Add_NewState_Expires()
        {
            // Arrange

            var tcs = new TaskCompletionSource<IMemoryOwner<byte>>();

            var operation = new Mock<IOperation>();
            operation
                .Setup(m => m.HandleOperationCompleted(It.IsAny<IMemoryOwner<byte>>()))
                .Callback((IMemoryOwner<byte> response) => tcs.TrySetResult(response));

            var state = MakeState(5, operation.Object);

            using var set = new InFlightOperationSet();

            // Act

            set.Add(state, 10);

            // Wait up to 15 seconds for the task to complete
            await Task.WhenAny(tcs.Task, Task.Delay(15000));

            // Assert

            Assert.True(tcs.Task.IsCompleted);

            using var data = tcs.Task.Result;
            var status = (ResponseStatus) ByteConverter.ToInt16(data.Memory.Span.Slice(HeaderOffsets.Status));
            Assert.Equal(ResponseStatus.OperationTimeout, status);
        }

        #endregion

        #region TryRemove

        [Fact]
        public void TryRemove_InSet_GetsAndRemoves()
        {
            // Arrange

            var state = MakeState(5);

            using var set = new InFlightOperationSet();
            set.Add(state, 75000);

            // Act

            var result = set.TryRemove(5, out var outState);

            // Assert

            Assert.True(result);
            Assert.Equal(state, outState);
            Assert.Equal(0, set.Count);
        }

        [Fact]
        public void TryRemove_InSet_DoesNotGet()
        {
            // Arrange

            var state = MakeState(5);

            using var set = new InFlightOperationSet();
            set.Add(state, 75000);

            // Act

            var result = set.TryRemove(6, out var outState);

            // Assert

            Assert.False(result);
            Assert.Null(outState);
            Assert.Equal(1, set.Count);
        }

        #endregion

        #region MyRegion

        [Fact]
        public void WaitForAllOperationsAsync_NoStates_NoWait()
        {
            // Arrange


            using var set = new InFlightOperationSet();

            // Act

            var task = set.WaitForAllOperationsAsync(TimeSpan.FromSeconds(10));

            // Assert

            Assert.True(task.IsCompleted);
        }

        [Fact]
        public async Task WaitForAllOperationsAsync_WithStates_WaitsOnAll()
        {
            // Arrange

            var state1 = MakeState(5);
            var state2 = MakeState(6);

            using var set = new InFlightOperationSet();
            set.Add(state1, 75000);
            set.Add(state2, 75000);

            // Act

            var task = set.WaitForAllOperationsAsync(TimeSpan.FromSeconds(10));

            // Assert

            await Task.Delay(10).ConfigureAwait(false);
            Assert.False(task.IsCompleted);

            state1.Complete(null);
            await Task.Delay(10).ConfigureAwait(false);
            Assert.False(task.IsCompleted);

            state2.Complete(null);
            await Task.Delay(10).ConfigureAwait(false);
            Assert.True(task.IsCompleted);
        }

        [Fact]
        public async Task WaitForAllOperationsAsync_WithTimeout_TimeOut()
        {
            // Arrange

            var state1 = MakeState(5);
            var state2 = MakeState(6);

            using var set = new InFlightOperationSet();
            set.Add(state1, 75000);
            set.Add(state2, 75000);

            // Act

            await Assert.ThrowsAsync<TimeoutException>(() => set.WaitForAllOperationsAsync(TimeSpan.FromMilliseconds(100)).AsTask());
        }

        #endregion

        #region Helpers

        private static AsyncState MakeState(uint opaque, IOperation operation = null) =>
            new AsyncState(operation ?? Mock.Of<IOperation>(op => op.Opaque == opaque));

        #endregion
    }
}
