#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Client.Transactions.Cleanup.LostTransactions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Couchbase.UnitTests.Transactions
{
    public class AtrBatchProcessorTests
    {
        [Fact]
        public void TimePerAtr_CalculatesCorrectly()
        {
            var processor = new AtrBatchProcessor(
                cleanupWindow: TimeSpan.FromSeconds(10),
                totalAtrs: 100);

            Assert.Equal(TimeSpan.FromMilliseconds(100), processor.TimePerAtr);
        }

        [Fact]
        public void TimePerAtr_With1024Atrs_CalculatesCorrectly()
        {
            var cleanupWindow = TimeSpan.FromSeconds(60);
            const int totalAtrs = 1024;

            var processor = new AtrBatchProcessor(cleanupWindow, totalAtrs);

            // 60s / 1024 = ~58.59ms. Must keep sub-ms precision, so check the ticks-based value
            // rather than whole-millisecond bounds.
            var expected = TimeSpan.FromTicks(cleanupWindow.Ticks / totalAtrs);
            Assert.Equal(expected, processor.TimePerAtr);
            Assert.InRange(processor.TimePerAtr.TotalMilliseconds, 58.5, 58.7);
        }

        [Theory]
        [InlineData(0, 0, 1)]      // Just started, on schedule -> batch of 1
        [InlineData(100, 1, 1)]    // 100ms elapsed, 1 processed (expected 1) -> on schedule
        [InlineData(500, 1, 5)]    // 500ms elapsed, 1 processed (expected 5) -> 4 behind, batch of 5
        [InlineData(1000, 1, 10)]  // 1000ms elapsed, 1 processed (expected 10) -> 9 behind, batch of 10
        [InlineData(2000, 1, 16)]  // 2000ms elapsed, 1 processed (expected 20) -> capped at 16
        [InlineData(500, 5, 1)]    // 500ms elapsed, 5 processed (expected 5) -> on schedule
        [InlineData(500, 10, 1)]   // 500ms elapsed, 10 processed (expected 5) -> ahead of schedule
        public void CalculateBatchSize_ReturnsExpectedSize(int elapsedMs, int processed, int expectedBatch)
        {
            var processor = new AtrBatchProcessor(
                cleanupWindow: TimeSpan.FromSeconds(10),
                totalAtrs: 100); // 100ms per ATR

            var batchSize = processor.CalculateBatchSize(
                elapsed: TimeSpan.FromMilliseconds(elapsedMs),
                atrsProcessed: processed);

            Assert.Equal(expectedBatch, batchSize);
        }

        [Fact]
        public void CalculateBatchSize_WithZeroTotalAtrs_ReturnsDefault()
        {
            var processor = new AtrBatchProcessor(
                cleanupWindow: TimeSpan.FromSeconds(10),
                totalAtrs: 0);

            var batchSize = processor.CalculateBatchSize(
                elapsed: TimeSpan.FromSeconds(5),
                atrsProcessed: 0);

            Assert.Equal(1, batchSize);
        }

        [Fact]
        public void CalculateBatchSize_WithRealisticValues_CalculatesCorrectly()
        {
            // Realistic scenario: 60s window, 1024 ATRs (~58.6ms per ATR)
            var processor = new AtrBatchProcessor(
                cleanupWindow: TimeSpan.FromSeconds(60),
                totalAtrs: 1024);

            // On schedule after 1 second (should have processed ~17 ATRs)
            var batchSize = processor.CalculateBatchSize(
                elapsed: TimeSpan.FromSeconds(1),
                atrsProcessed: 17);
            Assert.Equal(1, batchSize);

            // Behind schedule: 5 seconds elapsed but only 10 processed (expected ~85)
            batchSize = processor.CalculateBatchSize(
                elapsed: TimeSpan.FromSeconds(5),
                atrsProcessed: 10);
            Assert.True(batchSize > 10, $"Expected batch > 10 when significantly behind, got {batchSize}");
            Assert.True(batchSize <= 16, $"Expected batch <= 16 (max), got {batchSize}");
        }

        [Fact]
        public void CalculateBatchSize_NeverExceedsMax()
        {
            var processor = new AtrBatchProcessor(
                cleanupWindow: TimeSpan.FromSeconds(10),
                totalAtrs: 100);

            // Extremely behind - 10 seconds elapsed, nothing processed
            var batchSize = processor.CalculateBatchSize(
                elapsed: TimeSpan.FromSeconds(10),
                atrsProcessed: 0);

            Assert.Equal(16, batchSize); // Should be capped at max
        }

        [Fact]
        public void CalculateBatchSize_ReturnsOneWhenAhead()
        {
            var processor = new AtrBatchProcessor(
                cleanupWindow: TimeSpan.FromSeconds(10),
                totalAtrs: 100);

            // Way ahead - 1 second elapsed, 50 processed (expected ~10)
            var batchSize = processor.CalculateBatchSize(
                elapsed: TimeSpan.FromSeconds(1),
                atrsProcessed: 50);

            Assert.Equal(1, batchSize);
        }

        [Theory]
        [InlineData(1, 50, 50)]    // Batch of 1 took 50ms, budget 100ms -> delay 50ms
        [InlineData(1, 100, 0)]    // Batch of 1 took 100ms, budget 100ms -> no delay
        [InlineData(1, 150, 0)]    // Batch of 1 took 150ms, budget 100ms -> no delay (behind)
        [InlineData(2, 50, 150)]   // Batch of 2 took 50ms, budget 200ms -> delay 150ms
        [InlineData(5, 100, 400)]  // Batch of 5 took 100ms, budget 500ms -> delay 400ms
        public void CalculateDelayAfterBatch_ReturnsExpectedDelay(int batchSize, int durationMs, int expectedDelayMs)
        {
            var processor = new AtrBatchProcessor(
                cleanupWindow: TimeSpan.FromSeconds(10),
                totalAtrs: 100); // 100ms per ATR

            var delay = processor.CalculateDelayAfterBatch(
                batchSize: batchSize,
                batchDuration: TimeSpan.FromMilliseconds(durationMs));

            Assert.Equal(TimeSpan.FromMilliseconds(expectedDelayMs), delay);
        }

        [Fact]
        public void TakeBatch_TakesUpToBatchSize()
        {
            var bag = new ConcurrentBag<string>(new[] { "atr-1", "atr-2", "atr-3", "atr-4", "atr-5" });

            var batch = AtrBatchProcessor.TakeBatch(
                () => bag.TryTake(out var id) ? (true, id) : (false, null),
                batchSize: 3);

            Assert.Equal(3, batch.Count);
            Assert.Equal(2, bag.Count); // 2 remaining
        }

        [Fact]
        public void TakeBatch_ReturnsPartialWhenNotEnough()
        {
            var bag = new ConcurrentBag<string>(new[] { "atr-1", "atr-2" });

            var batch = AtrBatchProcessor.TakeBatch(
                () => bag.TryTake(out var id) ? (true, id) : (false, null),
                batchSize: 5);

            Assert.Equal(2, batch.Count);
            Assert.Empty(bag);
        }

        [Fact]
        public void TakeBatch_ReturnsEmptyWhenSourceEmpty()
        {
            var bag = new ConcurrentBag<string>();

            var batch = AtrBatchProcessor.TakeBatch(
                () => bag.TryTake(out var id) ? (true, id) : (false, null),
                batchSize: 5);

            Assert.Empty(batch);
        }

        [Fact]
        public void TakeBatch_ReturnsOnlyAvailableWhenBatchSizeExceedsRemaining()
        {
            // Simulates: 20 behind schedule (batchSize=16) but only 3 ATRs left
            var bag = new ConcurrentBag<string>(new[] { "atr-1", "atr-2", "atr-3" });

            var batch = AtrBatchProcessor.TakeBatch(
                () => bag.TryTake(out var id) ? (true, id) : (false, null),
                batchSize: 16);

            Assert.Equal(3, batch.Count);
            Assert.Empty(bag);
        }

        [Fact]
        public async Task ProcessBatchAsync_ProcessesSingleItem()
        {
            var processed = new List<string>();
            var batch = new List<string> { "atr-1" };

            await AtrBatchProcessor.ProcessBatchAsync(
                batch,
                (atrId, ct) => { processed.Add(atrId); return Task.CompletedTask; },
                CancellationToken.None);

            Assert.Single(processed);
            Assert.Equal("atr-1", processed[0]);
        }

        [Fact]
        public async Task ProcessBatchAsync_WithMultipleItems_ProcessesAll()
        {
            // Multiple items are dispatched via Task.WhenAll; confirm every item is processed.
            // Pass/fail depends only on the set of processed items - never on timing.
            var batch = new List<string> { "atr-1", "atr-2", "atr-3", "atr-4" };
            var processedItems = new ConcurrentBag<string>();

            await AtrBatchProcessor.ProcessBatchAsync(
                batch,
                async (atrId, ct) =>
                {
                    await Task.Yield(); // allow interleaving without depending on it
                    processedItems.Add(atrId);
                },
                CancellationToken.None);

            Assert.Equal(batch.Count, processedItems.Count);
            foreach (var item in batch)
            {
                Assert.Contains(item, processedItems);
            }
        }

        [Fact]
        public async Task ProcessBatchAsync_HandlesEmptyBatch()
        {
            var processed = new List<string>();

            await AtrBatchProcessor.ProcessBatchAsync(
                new List<string>(),
                (atrId, ct) => { processed.Add(atrId); return Task.CompletedTask; },
                CancellationToken.None);

            Assert.Empty(processed);
        }

        [Fact]
        public async Task ApplyDelayAsync_WhenCancelled_DoesNotDelay()
        {
            // A pre-cancelled token must short-circuit the delay synchronously - without ever consulting
            // the clock. The virtual clock is never advanced, so if the delay were actually awaited the
            // task would stay incomplete forever; asserting completion proves cancellation short-circuits.
            var time = new FakeTimeProvider();
            var processor = new AtrBatchProcessor(
                cleanupWindow: TimeSpan.FromSeconds(600),
                totalAtrs: 10, // 60 seconds per ATR - an enormous delay if it were honoured
                timeProvider: time);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var task = processor.ApplyDelayAsync(
                batchSize: 1,
                batchDuration: TimeSpan.Zero, // full 60s budget remaining
                cts.Token);

            Assert.True(task.IsCompleted); // returned immediately, no clock advance needed
            await task;                     // pre-cancel path returns normally, does not throw
        }

        [Fact]
        public async Task ApplyDelayAsync_DelaysAgainstInjectedClock()
        {
            // 10s window, 100 ATRs -> 100ms budget per ATR. A batch of 1 that took no time should wait
            // the full 100ms budget - but against virtual time, so the test never really sleeps.
            var time = new FakeTimeProvider();
            var processor = new AtrBatchProcessor(
                cleanupWindow: TimeSpan.FromSeconds(10),
                totalAtrs: 100,
                timeProvider: time);

            var task = processor.ApplyDelayAsync(
                batchSize: 1,
                batchDuration: TimeSpan.Zero,
                CancellationToken.None);

            Assert.False(task.IsCompleted); // still waiting out the 100ms budget

            time.Advance(TimeSpan.FromMilliseconds(100));

            await task; // returns (without throwing) once virtual time advances past the delay
            Assert.True(task.IsCompleted);
        }

        [Fact]
        public async Task ApplyDelayAsync_WhenBehindSchedule_DoesNotDelay()
        {
            // Batch took longer than its budget -> no delay, so the batching can sprint.
            var processor = new AtrBatchProcessor(
                cleanupWindow: TimeSpan.FromSeconds(10),
                totalAtrs: 100,
                timeProvider: new FakeTimeProvider());

            await processor.ApplyDelayAsync(
                batchSize: 1,
                batchDuration: TimeSpan.FromMilliseconds(500), // budget was 100ms
                CancellationToken.None); // returns immediately; no clock advance needed
        }
    }
}
