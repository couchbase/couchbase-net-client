using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Tracks a set of in-flight operations for <see cref="MultiplexingConnection"/>.
    /// </summary>
    internal class InFlightOperationSet : IDisposable
    {
        private readonly ConcurrentDictionary<uint, AsyncState> _statesInFlight = new();
        private volatile CancellationTokenSource? _cts = new();

        public int Count => _statesInFlight.Count;

        public TimeSpan Timeout { get; }
        public TimeSpan CleanupInterval { get; }

        /// <summary>
        /// Creates a new InFlightOperationSet.
        /// </summary>
        /// <param name="timeout">Timeout after which an operation is canceled.</param>
        /// <param name="cleanupInterval">How frequently the in flight set is scanned for orphaned operations.</param>
        public InFlightOperationSet(TimeSpan timeout, TimeSpan? cleanupInterval = null)
        {
            if (timeout <= TimeSpan.Zero)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(timeout));
            }
            if (cleanupInterval.HasValue && cleanupInterval.GetValueOrDefault() <= TimeSpan.Zero)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(cleanupInterval));
            }

            Timeout = timeout;
            CleanupInterval = cleanupInterval ?? TimeSpan.FromSeconds(30);

            using (ExecutionContext.SuppressFlow())
            {
                Task.Run(CleanupLoop);
            }
        }

        /// <summary>
        /// Adds a operation to the set.
        /// </summary>
        /// <param name="state">Operation to add.</param>
        public void Add(AsyncState state)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (state == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(state));
            }

            _statesInFlight.TryAdd(state.Opaque, state);
        }

        /// <summary>
        /// Try to remove an operation from the set.
        /// </summary>
        /// <param name="opaque">Opaque identifier of the operation to remove.</param>
        /// <param name="state">The operation state, if found.</param>
        /// <returns>True if the operation was found.</returns>
        public bool TryRemove(uint opaque, [NotNullWhen(true)] out AsyncState? state) =>
            _statesInFlight.TryRemove(opaque, out state);

        /// <summary>
        /// Wait for all currently in flight operations to complete.
        /// </summary>
        /// <param name="timeout">Timeout before the wait is canceled.</param>
        /// <returns>A task to observe for completion.</returns>
        /// <remarks>
        /// This method will not wait for any new operations added after it is run.
        /// </remarks>
        public async ValueTask WaitForAllOperationsAsync(TimeSpan timeout)
        {
            if (Count == 0)
            {
                return;
            }

            var allStatesTask = Task.WhenAll(
                _statesInFlight.Select(p => p.Value.CompletionTask));

            var waitTask = await Task.WhenAny(allStatesTask, Task.Delay(timeout)).ConfigureAwait(false);

            if (waitTask != allStatesTask)
            {
                throw new TimeoutException();
            }
        }

        /// <summary>
        /// Loop which runs until disposed to check for orphaned operations. Any found running
        /// more than the configured timeout are disposed and removed from the collection.
        /// </summary>
        private async Task CleanupLoop()
        {
            var cancellationToken = _cts?.Token ?? default;
            if (cancellationToken == default)
            {
                // Already disposed before the loop started
                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(CleanupInterval, cancellationToken).ConfigureAwait(false);

                try
                {
                    foreach (var stateInFlight in _statesInFlight.Values)
                    {
                        if (stateInFlight.TimeInFlight > Timeout)
                        {
                            if (_statesInFlight.TryRemove(stateInFlight.Opaque, out _))
                            {
                                stateInFlight.Dispose();
                            }
                        }
                    }
                }
                catch
                {
                    // Don't let exceptions kill the loop
                }
            }
        }

        public void Dispose()
        {
            var cts = Interlocked.Exchange(ref _cts, null);
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
            }

            // free up all states in flight
            lock (_statesInFlight)
            {
                foreach (var state in _statesInFlight.Values)
                {
                    state.Complete(SlicedMemoryOwner<byte>.Empty);
                    state.Dispose();
                }
            }
        }
    }
}
