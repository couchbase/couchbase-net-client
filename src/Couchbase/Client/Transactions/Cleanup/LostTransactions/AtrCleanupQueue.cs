#nullable enable
using System;
using System.Collections.Generic;

namespace Couchbase.Client.Transactions.Cleanup.LostTransactions
{
    /// <summary>
    /// Holds the ATRs this client still has to clean for the current lap, and the lap-scoped schedule state
    /// (elapsed time and progress) used to drive adaptive batching.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A "lap" is one complete pass over the ATRs assigned to this client. The lap is <em>supposed</em> to take
    /// one cleanup window, evenly paced. When a window is too short - or the host is CPU starved - a lap can spill
    /// across multiple windows: the caller bounds each pass by the window and the remainder simply stays in the
    /// queue to be resumed on the next pass, so no ATR is starved.
    /// </para>
    /// <para>
    /// Because the lap clock (<see cref="LapElapsed"/>) and progress (<see cref="LapProcessed"/>) persist across
    /// windows until a lap actually completes, a resumed lap reports itself as well behind schedule from its first
    /// batch - which is exactly what makes <see cref="AtrBatchProcessor"/> sprint to catch up rather than dribble.
    /// </para>
    /// <para>
    /// All access is single-pass and serialized by the caller's timer-callback mutex, so a plain
    /// <see cref="Queue{T}"/> is sufficient - no concurrent collection is required. Time comes from an injected
    /// <see cref="TimeProvider"/> so the resume/sprint/reset behaviour is deterministically unit-testable.
    /// </para>
    /// </remarks>
    internal sealed class AtrCleanupQueue
    {
        private readonly TimeProvider _timeProvider;

        private Queue<string> _queue = new();
        private long _lapStartTimestamp;
        private long _lapProcessed;

        // Identity of the assigned set the current queue was built from. When the topology changes
        // (a client joins/leaves), this client's slice of ATRs changes and the lap must be rebuilt.
        private int _stampIndexOfThisClient = -1;
        private int _stampNumActiveClients = -1;

        public AtrCleanupQueue(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        /// <summary>Number of ATRs left to clean in the current lap.</summary>
        public int Remaining => _queue.Count;

        /// <summary>ATRs cleaned so far in the current lap (persists across windows until the lap completes).</summary>
        public long LapProcessed => _lapProcessed;

        /// <summary>Wall-clock time since the current lap started (persists across windows until the lap completes).</summary>
        public TimeSpan LapElapsed => _timeProvider.GetElapsedTime(_lapStartTimestamp);

        /// <summary>True when there is nothing left to clean in the current lap.</summary>
        public bool LapComplete => _queue.Count == 0;

        /// <summary>
        /// Called at the start of each cleanup pass with this client's freshly-computed ATR assignment.
        /// Starts a new lap (refilling the queue and resetting the lap clock and progress) when the topology
        /// changed or the previous lap completed; otherwise resumes the in-progress lap, leaving the queue
        /// remainder and lap clock untouched.
        /// </summary>
        public void SyncLap(IReadOnlyList<string> atrsHandledByThisClient, int indexOfThisClient, int numActiveClients)
        {
            var topologyChanged = indexOfThisClient != _stampIndexOfThisClient
                                  || numActiveClients != _stampNumActiveClients;

            if (!topologyChanged && _queue.Count > 0)
            {
                // Resume the lap that a previous window left unfinished - keep the remainder and the lap clock.
                return;
            }

            _queue = new Queue<string>(atrsHandledByThisClient);
            _stampIndexOfThisClient = indexOfThisClient;
            _stampNumActiveClients = numActiveClients;
            _lapProcessed = 0;
            _lapStartTimestamp = _timeProvider.GetTimestamp();
        }

        /// <summary>
        /// Removes up to <paramref name="batchSize"/> ATRs from the front of the queue. Returns fewer than
        /// requested (possibly none) when the queue drains - it never refills, so a batch only ever contains
        /// distinct ids that can be cleaned concurrently without cleaning the same ATR twice.
        /// </summary>
        public List<string> TakeBatch(int batchSize) =>
            AtrBatchProcessor.TakeBatch(
                () => _queue.Count > 0 ? (true, _queue.Dequeue()) : (false, (string?)null),
                batchSize);

        /// <summary>Records that <paramref name="count"/> ATRs were cleaned, advancing lap progress.</summary>
        public void RecordCleaned(int count) => _lapProcessed += count;
    }
}
