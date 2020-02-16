using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Utils
{
    /// <summary>
    /// Asynchronous mutex. Higher performance than <see cref="SemaphoreSlim"/> when working with use cases
    /// where there is rarely contention.
    /// </summary>
    internal class AsyncMutex
    {
        private readonly Queue<TaskCompletionSource<bool>> _tcsQueue = new Queue<TaskCompletionSource<bool>>();
        private bool _isLocked;

        /// <summary>
        /// Creates a lock to prevent multiple simultaneous operations. High performance in the case
        /// where there is no contention, but does support a fallback when there is contention.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token to stop waiting.</param>
        /// <returns>Task to await.</returns>
        public ValueTask GetLockAsync(CancellationToken cancellationToken = default)
        {
            lock (_tcsQueue)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!_isLocked)
                {
                    // Fast
                    _isLocked = true;
                    return default;
                }
                else
                {
                    // Slower
                    var tcs = new TaskCompletionSource<bool>();
                    _tcsQueue.Enqueue(tcs);

                    if (cancellationToken.CanBeCanceled)
                    {
                        var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

                        // Use state instead of local vars to reduce heap allocations due to closures
                        tcs.Task.ContinueWith((completedTask, state) => {
                           ((CancellationTokenRegistration) state).Dispose();
                        }, registration, cancellationToken);
                    }

                    return new ValueTask(tcs.Task);
                }
            }
        }

        /// <summary>
        /// Releases a currently held lock.
        /// </summary>
        public void ReleaseLock()
        {
            lock (_tcsQueue)
            {
                if (_isLocked)
                {
                    if (_tcsQueue.Count > 0)
                    {
                        var tcs = _tcsQueue.Dequeue();

                        Task.Factory.StartNew(() =>
                        {
                            if (!tcs.TrySetResult(true))
                            {
                                // Was cancelled, we need to get the next item in the queue
                                ReleaseLock();
                            }
                        },
                        CancellationToken.None, TaskCreationOptions.PreferFairness, TaskScheduler.Default);
                    }
                    else
                    {
                        _isLocked = false;
                    }
                }
            }
        }
    }
}
