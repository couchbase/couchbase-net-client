using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class AsyncMutexTests
    {
        [Fact]
        public void GetLockAsync_NoLock_ReturnsCompletedTask()
        {
            // Arrange

            var mutex = new AsyncMutex();

            // Act

            var task = mutex.GetLockAsync();

            // Assert

            Assert.True(task.IsCompleted);
        }

        [Fact]
        public async Task GetLockAsync_TwoLocks_TriggersSecondWhenReleased()
        {
            // Arrange

            var mutex = new AsyncMutex();

            // Act/Assert

            var task = mutex.GetLockAsync();
            Assert.True(task.IsCompleted);
            var task2 = mutex.GetLockAsync();
            Assert.False(task2.IsCompleted);

            mutex.ReleaseLock();

            await task2.ConfigureAwait(false);
        }

        [Fact]
        public void GetLockAsync_TwoLocksWithReleases_ReturnsCompletedTasks()
        {
            // Arrange

            var mutex = new AsyncMutex();

            // Act/Assert

            var task = mutex.GetLockAsync();
            Assert.True(task.IsCompleted);
            mutex.ReleaseLock();

            var task2 = mutex.GetLockAsync();
            Assert.True(task2.IsCompleted);
        }

        [Fact]
        public async Task GetLockAsync_WithCancellationToken_Cancels()
        {
            // Arrange

            var mutex = new AsyncMutex();
            await mutex.GetLockAsync().ConfigureAwait(false);

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

            // Act/Assert

            var ex = await Assert.ThrowsAsync<TaskCanceledException>(() => mutex.GetLockAsync(cts.Token).AsTask()).ConfigureAwait(false);

            Assert.Equal(cts.Token, ex.CancellationToken);
        }
    }
}
