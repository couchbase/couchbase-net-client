using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.UnitTests.Utils;
using Couchbase.Utils;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Connections
{
    public class InFlightOperationSetTests
    {
        #region AddAsync

        [Fact]
        public async Task AddAsync_NewState_AddsToCount()
        {
            // Arrange

            var state = MakeState(5);

            using var set = new InFlightOperationSet(8, TimeSpan.FromSeconds(75));

            // Act

            await set.AddAsync(state);

            // Assert

            Assert.Equal(1, set.Count);
        }

        [Fact]
        public async Task AddAsync_PastMaximum_Waits()
        {
            // Arrange

            var state = MakeState(5);

            using var set = new InFlightOperationSet(2, TimeSpan.FromSeconds(75));
            await set.AddAsync(MakeState(6));
            await set.AddAsync(MakeState(7));

            // Act

            var addTask = set.AddAsync(state);
            await Task.Delay(10);
            Assert.False(addTask.IsCompleted);

            Assert.True(set.TryRemove(6, out _));

            // Wait up to 15 seconds for the task to complete
            var whichCompleted = await Task.WhenAny(addTask, Task.Delay(15000));

            // Assert

            Assert.Equal(addTask, whichCompleted);
        }

        [Fact]
        public async Task AddAsync_NewState_Expires()
        {
            // Arrange

            var operation = new FakeOperation();

            var state = MakeState(5, operation);

            using var set = new InFlightOperationSet(8, TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(1));

            // Act

            await set.AddAsync(state);

            // Wait up to 15 seconds for the task to complete
            await Task.WhenAny(state.CompletionTask, Task.Delay(15000));

            // Assert

            Assert.True(state.CompletionTask.IsCompleted);
        }

        #endregion

        #region TryRemove

        [Theory]
        [InlineData(0)]
        [InlineData(7)]
        public async Task TryRemove_InSet_GetsAndRemoves(uint extraStates)
        {
            // Arrange

            uint opaque = extraStates;
            var state = MakeState(opaque);

            using var set = new InFlightOperationSet(8, TimeSpan.FromSeconds(75));
            for (uint i = 0; i < extraStates; i++)
            {
                await set.AddAsync(MakeState(i));
            }

            await set.AddAsync(state);

            // Act

            var result = set.TryRemove(opaque, out var outState);

            // Assert

            Assert.True(result);
            Assert.Equal(state, outState);
            Assert.Equal((int) extraStates, set.Count);
        }

        [Fact]
        public async Task TryRemove_NotInSet_DoesNotGet()
        {
            // Arrange

            var state = MakeState(5);

            using var set = new InFlightOperationSet(8, TimeSpan.FromSeconds(75));
            await set.AddAsync(state);

            // Act

            var result = set.TryRemove(6, out var outState);

            // Assert

            Assert.False(result);
            Assert.Null(outState);
            Assert.Equal(1, set.Count);
        }

        #endregion

        #region TryGet

        [Theory]
        [InlineData(0)]
        [InlineData(7)]
        public async Task TryGet_InSet_GetsAndDoesNotRemove(uint extraStates)
        {
            // Arrange

            uint opaque = extraStates;
            var state = MakeState(opaque);

            using var set = new InFlightOperationSet(8, TimeSpan.FromSeconds(75));
            for (uint i = 0; i < extraStates; i++)
            {
                await set.AddAsync(MakeState(i));
            }

            await set.AddAsync(state);

            // Act

            var result = set.TryGet(opaque, out var stateMatch);

            // Assert

            Assert.True(result);
            Assert.Equal(state, stateMatch);
            Assert.Equal((int) extraStates + 1, set.Count);
        }

        [Fact]
        public async Task TryGet_NotInSet_DoesNotGet()
        {
            // Arrange

            var state = MakeState(5);

            using var set = new InFlightOperationSet(8, TimeSpan.FromSeconds(75));
            await set.AddAsync(state);

            // Act

            var result = set.TryGet(6, out var outState);

            // Assert

            Assert.False(result);
            Assert.Equal(1, set.Count);
        }

        #endregion

        #region MyRegion

        [Fact]
        public void WaitForAllOperationsAsync_NoStates_NoWait()
        {
            // Arrange


            using var set = new InFlightOperationSet(8, TimeSpan.FromSeconds(75));

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

            using var set = new InFlightOperationSet(8, TimeSpan.FromSeconds(75));
            await set.AddAsync(state1);
            await set.AddAsync(state2);

            // Act

            var task = set.WaitForAllOperationsAsync(TimeSpan.FromSeconds(10));

            // Assert

            await Task.Delay(10).ConfigureAwait(false);
            Assert.False(task.IsCompleted, userMessage: "Task should not be complete before any states are complete.");

            state1.Complete(SlicedMemoryOwner<byte>.Empty);
            await Task.Delay(10).ConfigureAwait(false);
            Assert.False(task.IsCompleted, userMessage: "Task should not be complete before all states are complete");

            state2.Complete(SlicedMemoryOwner<byte>.Empty);

            await Task.WhenAny(task.AsTask(), Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.True(task.IsCompleted, userMessage: "Task should have been complete within a reasonable amount of time after all states were complete.");
        }

        [Fact]
        public async Task WaitForAllOperationsAsync_WithTimeout_TimeOut()
        {
            // Arrange

            var state1 = MakeState(5);
            var state2 = MakeState(6);

            using var set = new InFlightOperationSet(8, TimeSpan.FromSeconds(75));
            await set.AddAsync(state1);
            await set.AddAsync(state2);

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
