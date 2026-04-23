#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Client.Transactions.Cleanup.LostTransactions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Couchbase.UnitTests.Transactions
{
    public class AtrCleanupQueueTests
    {
        private static List<string> Atrs(int count) =>
            Enumerable.Range(0, count).Select(i => $"atr-{i}").ToList();

        [Fact]
        public void SyncLap_FirstCall_FillsQueueAndStartsLap()
        {
            var queue = new AtrCleanupQueue(new FakeTimeProvider());

            queue.SyncLap(Atrs(100), indexOfThisClient: 0, numActiveClients: 1);

            Assert.Equal(100, queue.Remaining);
            Assert.Equal(0, queue.LapProcessed);
            Assert.False(queue.LapComplete);
        }

        [Fact]
        public void TakeBatch_DequeuesInOrderUpToBatchSize()
        {
            var queue = new AtrCleanupQueue(new FakeTimeProvider());
            queue.SyncLap(new List<string> { "a", "b", "c", "d", "e" }, 0, 1);

            Assert.Equal(new[] { "a", "b", "c" }, queue.TakeBatch(3));
            Assert.Equal(new[] { "d", "e" }, queue.TakeBatch(3)); // partial when fewer remain
            Assert.Empty(queue.TakeBatch(3));
        }

        [Fact]
        public void SyncLap_WithItemsRemainingAndSameTopology_ResumesWithoutRefilling()
        {
            var time = new FakeTimeProvider();
            var queue = new AtrCleanupQueue(time);
            queue.SyncLap(Atrs(100), 0, 1);

            // Clean 60, leaving 40 - as if a window expired mid-lap.
            queue.TakeBatch(60);
            queue.RecordCleaned(60);
            time.Advance(TimeSpan.FromSeconds(10));

            // Next pass: same topology, items remaining -> resume, do NOT rebuild.
            queue.SyncLap(Atrs(100), 0, 1);

            Assert.Equal(40, queue.Remaining);
            Assert.Equal(60, queue.LapProcessed);                       // progress preserved
            Assert.True(queue.LapElapsed >= TimeSpan.FromSeconds(10));   // lap clock preserved (not reset)
        }

        [Fact]
        public void SyncLap_AfterLapComplete_StartsFreshLap()
        {
            var time = new FakeTimeProvider();
            var queue = new AtrCleanupQueue(time);
            queue.SyncLap(Atrs(100), 0, 1);

            queue.TakeBatch(100);
            queue.RecordCleaned(100);
            time.Advance(TimeSpan.FromSeconds(10));
            Assert.True(queue.LapComplete);

            // Next pass: lap finished -> refill and reset lap clock + progress.
            queue.SyncLap(Atrs(100), 0, 1);

            Assert.Equal(100, queue.Remaining);
            Assert.Equal(0, queue.LapProcessed);
            Assert.True(queue.LapElapsed < TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void SyncLap_WhenTopologyChanges_RebuildsLapEvenWithItemsRemaining()
        {
            var time = new FakeTimeProvider();
            var queue = new AtrCleanupQueue(time);

            // Index 0 of 1 client -> owns all 100.
            queue.SyncLap(Atrs(100), indexOfThisClient: 0, numActiveClients: 1);
            queue.TakeBatch(40);
            queue.RecordCleaned(40);
            time.Advance(TimeSpan.FromSeconds(5));

            // A second client joined: now index 0 of 2 -> owns a smaller, different slice. Rebuild.
            var newSlice = Atrs(50);
            queue.SyncLap(newSlice, indexOfThisClient: 0, numActiveClients: 2);

            Assert.Equal(50, queue.Remaining);                          // rebuilt from the new assignment
            Assert.Equal(0, queue.LapProcessed);                        // lap reset
            Assert.True(queue.LapElapsed < TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void ResumedLap_ReportsBehindSchedule_SoBatchingSprints()
        {
            // 10s window, 100 ATRs -> 100ms/ATR. A lap that has used a full window but cleaned only 50
            // should drive the batch processor to its max size when resumed.
            var time = new FakeTimeProvider();
            var queue = new AtrCleanupQueue(time);
            var processor = new AtrBatchProcessor(TimeSpan.FromSeconds(10), totalAtrs: 100, timeProvider: time);

            queue.SyncLap(Atrs(100), 0, 1);
            queue.TakeBatch(50);
            queue.RecordCleaned(50);
            time.Advance(TimeSpan.FromSeconds(10)); // a whole window elapsed, only half done

            queue.SyncLap(Atrs(100), 0, 1); // resume

            var batchSize = processor.CalculateBatchSize(queue.LapElapsed, queue.LapProcessed);

            Assert.Equal(16, batchSize); // capped max -> sprinting to catch up
        }

        [Fact]
        public void FreshLap_OnSchedule_UsesSingleBatches()
        {
            var time = new FakeTimeProvider();
            var queue = new AtrCleanupQueue(time);
            var processor = new AtrBatchProcessor(TimeSpan.FromSeconds(10), totalAtrs: 100, timeProvider: time);

            queue.SyncLap(Atrs(100), 0, 1);

            var batchSize = processor.CalculateBatchSize(queue.LapElapsed, queue.LapProcessed);

            Assert.Equal(1, batchSize); // just started, on schedule
        }

        [Fact]
        public void EmptyAssignment_IsImmediatelyComplete()
        {
            var queue = new AtrCleanupQueue(new FakeTimeProvider());

            queue.SyncLap(new List<string>(), indexOfThisClient: -1, numActiveClients: 1);

            Assert.Equal(0, queue.Remaining);
            Assert.True(queue.LapComplete);
            Assert.Empty(queue.TakeBatch(16));
        }

        [Fact]
        public void Constructor_NullTimeProvider_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new AtrCleanupQueue(null!));
        }
    }
}
