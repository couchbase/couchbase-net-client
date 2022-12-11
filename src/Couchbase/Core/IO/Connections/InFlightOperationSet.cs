using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        // This approach for storing AsyncState is optimized for a small number of MaximumOperations. It avoids the overhead
        // of a dictionary and allows lock-free operations using Interlocked. However, a ConcurrentDictionary may outperform
        // this approach if the maximum is large due to the cost of iterating the list. If we find the need for large maximums
        // we should reevaluate the approach.
        private readonly AsyncStateBase?[] _statesInFlight;

        // Limits the number of in-flight operations in a non-blocking manner. Operations should be added to the collection only
        // after waiting on the semaphore and operations should be removed from the collection before releasing the semaphore.
        private readonly SemaphoreSlim _semaphore;
        private volatile CancellationTokenSource? _cts = new();

        /// <summary>
        /// Number of currently in-flight operations.
        /// </summary>
        public int Count => MaximumOperations - _semaphore.CurrentCount;

        /// <summary>
        /// Maximum number of operations which may be in-flight.
        /// </summary>
        public int MaximumOperations { get; }

        /// <summary>
        /// How long an orphaned operation is left in the in-flight list before being removed by the cleanup process.
        /// </summary>
        public TimeSpan Timeout { get; }

        /// <summary>
        /// How frequently the cleanup process checks for operations past the <see cref="Timeout"/>.
        /// </summary>
        public TimeSpan CleanupInterval { get; }

        /// <summary>
        /// Creates a new InFlightOperationSet.
        /// </summary>
        /// <param name="maximumOperations">Maximum number of operations which may be in-flight.</param>
        /// <param name="timeout">Timeout after which an operation is canceled.</param>
        /// <param name="cleanupInterval">How frequently the in flight set is scanned for orphaned operations.</param>
        public InFlightOperationSet(int maximumOperations, TimeSpan timeout, TimeSpan? cleanupInterval = null)
        {
            if (maximumOperations <= 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(maximumOperations));
            }
            if (timeout <= TimeSpan.Zero)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(timeout));
            }
            if (cleanupInterval.HasValue && cleanupInterval.GetValueOrDefault() <= TimeSpan.Zero)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(cleanupInterval));
            }

            MaximumOperations = maximumOperations;
            Timeout = timeout;
            CleanupInterval = cleanupInterval ?? TimeSpan.FromSeconds(30);

            _statesInFlight = new AsyncStateBase?[maximumOperations];
            _semaphore = new SemaphoreSlim(maximumOperations);

            bool restoreFlow = false;
            try
            {
                if (!ExecutionContext.IsFlowSuppressed())
                {
                    ExecutionContext.SuppressFlow();
                    restoreFlow = true;
                }

                _ = CleanupLoop();
            }
            finally
            {
                if (restoreFlow)
                {
                    ExecutionContext.RestoreFlow();
                }
            }
        }

        /// <summary>
        /// Adds a operation to the set once there is room available.
        /// </summary>
        /// <param name="state">Operation to add.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task AddAsync(AsyncStateBase state, CancellationToken cancellationToken = default)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (state is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(state));
            }

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

            var added = false;
            try
            {
                // The semaphore ensures we have room, now find the first node in the list that is currently null
                // in a lock-free manner
                for (var i = 0; i < _statesInFlight.Length; i++)
                {
                    var previousValue = Interlocked.CompareExchange(ref _statesInFlight[i], state, null);
                    if (previousValue is null)
                    {
                        added = true;
                        break;
                    }
                }
            }
            finally
            {
                if (!added)
                {
                    // This path shouldn't happen because of the semaphore, but we'll check just to be safe
                    _semaphore.Release();
                    ThrowHelper.ThrowInvalidOperationException(
                        "Unable to add AsyncState to InFlightOperationSet, semaphore is out of sync.");
                }
            }
        }

        /// <summary>
        /// Try to remove an operation from the set.
        /// </summary>
        /// <param name="opaque">Opaque identifier of the operation to remove.</param>
        /// <param name="state">The operation state, if found.</param>
        /// <returns>True if the operation was found.</returns>
        public bool TryRemove(uint opaque, [NotNullWhen(true)] out AsyncStateBase? state)
        {
            for (var i = 0; i < _statesInFlight.Length; i++)
            {
                var possibleMatch = Volatile.Read(ref _statesInFlight[i]);
                if (possibleMatch?.Opaque == opaque)
                {
                    // We found a possible match, try to remove it in a thread-safe way
                    if (Interlocked.CompareExchange(ref _statesInFlight[i], null, possibleMatch) == possibleMatch)
                    {
                        _semaphore.Release();
                        state = possibleMatch;
                        return true;
                    }
                }
            }

            state = null;
            return false;
        }

        /// <summary>
        /// Try to get an operation from the set.
        /// </summary>
        /// <param name="opaque">Opaque identifier of the operation to get.</param>
        /// <param name="stateMatch">The operation state, if found, with information necessary to remove it.</param>
        /// <returns>True if the operation was found.</returns>
        public bool TryGet(uint opaque, out StateMatch stateMatch)
        {
            for (var i = 0; i < _statesInFlight.Length; i++)
            {
                var possibleMatch = Volatile.Read(ref _statesInFlight[i]);
                if (possibleMatch?.Opaque == opaque)
                {
                    stateMatch = new StateMatch(this, possibleMatch, i);
                    return true;
                }
            }

            stateMatch = default;
            return false;
        }

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

            List<Task> completionTasks = new(Count);
            for (var i = 0; i < _statesInFlight.Length; i++)
            {
                var state = Volatile.Read(ref _statesInFlight[i]);
                if (state is not null)
                {
                    completionTasks.Add(state.CompletionTask);
                }
            }

            var allStatesTask = Task.WhenAll(completionTasks);

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
                try
                {
                    await Task.Delay(CleanupInterval, cancellationToken).ConfigureAwait(false);

                    if (Count == 0)
                    {
                        continue;
                    }

                    for (var i = 0; i < _statesInFlight.Length; i++)
                    {
                        var state = Volatile.Read(ref _statesInFlight[i]);
                        if (state?.TimeInFlight > Timeout)
                        {
                            // Remove the state in a thread-safe manner
                            var removedState = Interlocked.CompareExchange(ref _statesInFlight[i], null, state);
                            if (ReferenceEquals(state, removedState))
                            {
                                _semaphore.Release();
                                state.Dispose();
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

            _semaphore.Dispose();

            for (var i = 0; i < _statesInFlight.Length; i++)
            {
                // free up all states in flight
                var state = Interlocked.Exchange(ref _statesInFlight[i], null);
                if (state is not null)
                {
                    state.Complete(SlicedMemoryOwner<byte>.Empty);
                    state.Dispose();
                }
            }
        }

        public readonly struct StateMatch
        {
            public InFlightOperationSet OperationSet { get; }
            public AsyncStateBase State { get; }
            public int Index { get; }

            public StateMatch(InFlightOperationSet operationSet, AsyncStateBase state, int index)
            {
                OperationSet = operationSet;
                State = state;
                Index = index;
            }

            /// <summary>
            /// Remove the State from the operation set if it is still present.
            /// </summary>
            public void Remove()
            {
                if (State is null)
                {
                    // Just in case this is the zero-init default StateMatch
                    return;
                }

                if (Interlocked.CompareExchange(ref OperationSet._statesInFlight[Index], null, State) == State)
                {
                    OperationSet._semaphore.Release();
                }
            }
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
