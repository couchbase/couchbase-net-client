#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Client.Transactions.Cleanup.LostTransactions
{
    /// <summary>
    /// Handles batched processing of ATRs with adaptive batch sizing based on schedule adherence.
    /// </summary>
    /// <remarks>
    /// Pure scheduling logic plus an injected <see cref="TimeProvider"/> for the pacing delay - holds no
    /// per-lap state of its own (that lives in <see cref="AtrCleanupQueue"/>), so it is safe to recreate
    /// per cleanup pass and is trivially unit-testable with a fake time provider.
    /// </remarks>
    internal class AtrBatchProcessor
    {
        private readonly TimeSpan _cleanupWindow;
        private readonly int _totalAtrs;
        private readonly TimeProvider _timeProvider;

        // Configurable limits - could be exposed via config if needed
        private const int MinBatchSize = 1;
        private const int MaxBatchSize = 16;
        private const int DefaultBatchSize = 1;

        public AtrBatchProcessor(TimeSpan cleanupWindow, int totalAtrs, TimeProvider? timeProvider = null)
        {
            _cleanupWindow = cleanupWindow;
            _totalAtrs = totalAtrs;
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <summary>
        /// Time budget per ATR based on cleanup window and total ATR count.
        /// </summary>
        /// <remarks>
        /// Uses ticks because TimeSpan.FromMilliseconds rounds to whole milliseconds on .NET Framework.
        /// </remarks>
        public TimeSpan TimePerAtr => TimeSpan.FromTicks(_cleanupWindow.Ticks / _totalAtrs);

        /// <summary>
        /// Calculates the appropriate batch size based on how far behind schedule we are.
        /// </summary>
        /// <param name="elapsed">Total time elapsed since cycle started</param>
        /// <param name="atrsProcessed">Number of ATRs processed so far</param>
        /// <returns>Recommended batch size (1 = on schedule, >1 = behind schedule)</returns>
        public int CalculateBatchSize(TimeSpan elapsed, long atrsProcessed)
        {
            if (_totalAtrs <= 0) return DefaultBatchSize;

            // How many ATRs should we have processed by now?
            var expectedProgress = (long)(elapsed.TotalMilliseconds / TimePerAtr.TotalMilliseconds);

            // How far behind are we?
            var behind = expectedProgress - atrsProcessed;

            if (behind <= 0)
            {
                // On schedule or ahead - process one at a time
                return MinBatchSize;
            }

            // Behind schedule - batch to catch up, but cap at MaxBatchSize
            return (int)Math.Min(behind + 1, MaxBatchSize);
        }

        /// <summary>
        /// Calculates how long to delay after processing a batch to stay on schedule.
        /// </summary>
        /// <param name="batchSize">Number of ATRs just processed</param>
        /// <param name="batchDuration">How long the batch took</param>
        /// <returns>Delay before next batch (may be zero if behind schedule)</returns>
        public TimeSpan CalculateDelayAfterBatch(int batchSize, TimeSpan batchDuration)
        {
            // Time budget for this batch
            var budgetForBatch = TimeSpan.FromTicks(TimePerAtr.Ticks * batchSize);

            // How much time remains in our budget?
            var remaining = budgetForBatch - batchDuration;

            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        /// <summary>
        /// Takes a batch of ATR IDs from the provided source.
        /// </summary>
        /// <param name="tryTakeNext">Function to get next ATR ID, returns false when empty</param>
        /// <param name="batchSize">Maximum batch size to take</param>
        /// <returns>List of ATR IDs (may be smaller than batchSize if source runs out)</returns>
        public static List<string> TakeBatch(Func<(bool success, string? atrId)> tryTakeNext, int batchSize)
        {
            var batch = new List<string>(batchSize);
            for (int i = 0; i < batchSize; i++)
            {
                var (success, atrId) = tryTakeNext();
                if (!success || atrId == null) break;
                batch.Add(atrId);
            }
            return batch;
        }

        /// <summary>
        /// Processes a batch of ATRs in parallel.
        /// </summary>
        public static async Task ProcessBatchAsync(
            IReadOnlyList<string> batch,
            Func<string, CancellationToken, Task> processAtr,
            CancellationToken cancellationToken)
        {
            if (batch.Count == 0) return;

            if (batch.Count == 1)
            {
                await processAtr(batch[0], cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var tasks = batch.Select(atrId => processAtr(atrId, cancellationToken));
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Applies the appropriate delay after processing a batch to maintain schedule.
        /// </summary>
        /// <remarks>
        /// Delegates the pacing delay to the injected <see cref="TimeProvider"/> so it runs against that
        /// clock - real time in production, virtual time under a fake provider in
        /// tests. When behind schedule the delay is zero and this returns immediately, which is what lets the
        /// adaptive batching sprint to catch up.
        /// </remarks>
        public async Task ApplyDelayAsync(int batchSize, TimeSpan batchDuration, CancellationToken cancellationToken)
        {
            var delay = CalculateDelayAfterBatch(batchSize, batchDuration);
            if (delay <= TimeSpan.Zero || cancellationToken.IsCancellationRequested) return;

            await _timeProvider.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }
}
