using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Tracks a set of in-flight operations for <see cref="MultiplexingConnection"/>.
    /// </summary>
    internal class InFlightOperationSet : IDisposable
    {
        private readonly ConcurrentDictionary<uint, AsyncState> _statesInFlight =
            new ConcurrentDictionary<uint, AsyncState>();

        public int Count => _statesInFlight.Count;

        /// <summary>
        /// Adds a operation to the set.
        /// </summary>
        /// <param name="state">Operation to add.</param>
        /// <param name="timeoutMilliseconds">Automatically cancel the operation, and remove it from the set, after this timeout is exceeded.</param>
        public void Add(AsyncState state, int timeoutMilliseconds)
        {
            if (state == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(state));
            }

            _statesInFlight.TryAdd(state.Opaque, state);

            state.Timer = new Timer(TimeoutHandler, state, timeoutMilliseconds, Timeout.Infinite);
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

        private void TimeoutHandler(object? state)
        {
            AsyncState a = (AsyncState) state!;
            _statesInFlight.TryRemove(a.Opaque, out _);
            a.Cancel(ResponseStatus.OperationTimeout);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // free up all states in flight
            lock (_statesInFlight)
            {
                foreach (var state in _statesInFlight.Values)
                {
                    state.Complete(null);
                    state.Dispose();
                }
            }
        }
    }
}
