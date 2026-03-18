#nullable enable
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.Diagnostics.Metrics;

[Collection("AppTelemetry")]
public class AppTelemetryThreadSafetyTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    private const string Node = "node1.example.com";
    private const string NodeUuid = "abc123";
    private const string Bucket = "travel-sample";

    public AppTelemetryThreadSafetyTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    /// <summary>
    /// Verifies that concurrent histogram increments don't lose any data.
    /// Multiple threads record metrics simultaneously, and the total count and sum
    /// must match exactly what was recorded.
    /// </summary>
    [Fact]
    public async Task ConcurrentHistogramIncrements_NoDataLoss()
    {
        var bins = new AppTelemetryHistogramBins(AppTelemetryRequestType.KvRetrieval);
        const int threadCount = 8;
        const int incrementsPerThread = 10_000;
        const double valueMs = 5.0;

        var barrier = new Barrier(threadCount);
        var tasks = new Task[threadCount];
        for (var i = 0; i < threadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                for (var j = 0; j < incrementsPerThread; j++)
                {
                    bins.IncrementCountAndSum(TimeSpan.FromMilliseconds(valueMs));
                }
            });
        }
        await Task.WhenAll(tasks);

        long totalCount = 0;
        double totalSum = 0;
        foreach (var bin in bins.SnapshotAndReset())
        {
            totalCount += bin.Count;
            totalSum += bin.SumMilliseconds;
        }

        var expectedCount = threadCount * incrementsPerThread;
        var expectedSum = expectedCount * valueMs;

        Assert.Equal(expectedCount, totalCount);
        Assert.Equal(expectedSum, totalSum, precision: 1);
    }

    /// <summary>
    /// Verifies that concurrent counter increments don't lose any data.
    /// </summary>
    [Fact]
    public async Task ConcurrentCounterIncrements_NoDataLoss()
    {
        var counterValue = new AppTelemetryCounterValue();
        const int threadCount = 10;
        const int incrementsPerThread = 100_000;

        var barrier = new Barrier(threadCount);
        var tasks = new Task[threadCount];
        for (var i = 0; i < threadCount; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                for (var j = 0; j < incrementsPerThread; j++)
                {
                    counterValue.Increment(AppTelemetryCounterType.Total);
                }
            });
        }
        await Task.WhenAll(tasks);

        var expectedTotal = threadCount * incrementsPerThread;
        var (_, _, total) = counterValue.SnapshotAndReset();
        Assert.Equal(expectedTotal, total);
    }

    /// <summary>
    /// Scenario A: Verifies that concurrent increments and exports don't lose data.
    /// Calls collector.IncrementMetrics directly to avoid interference from other tests
    /// that may register/unregister the static MetricTracker.AppTelemetry._collector.
    /// </summary>
    [Fact]
    public async Task IncrementDuringExport_NoMetricsLost()
    {
        using var collector = new AppTelemetryCollector();
        collector.Enable();

        const int writerCount = 10;
        const int incrementsPerWriter = 500_000;
        const int exportCount = 2000;

        var totalExpectedIncrements = writerCount * incrementsPerWriter;
        var allExported = new ConcurrentBag<string>();

        var writerTasks = new Task[writerCount];
        for (var i = 0; i < writerCount; i++)
        {
            writerTasks[i] = Task.Run(() =>
            {
                for (var j = 0; j < incrementsPerWriter; j++)
                {
                    collector.IncrementMetrics(
                        TimeSpan.FromMilliseconds(50),
                        Node, null, NodeUuid,
                        AppTelemetryServiceType.KeyValue,
                        AppTelemetryCounterType.Total,
                        AppTelemetryRequestType.KvRetrieval,
                        Bucket);
                }
            });
        }

        var exportTask = Task.Run(() =>
        {
            for (var e = 0; e < exportCount; e++)
            {
                if (collector.TryExportMetricsAndReset(out var result))
                {
                    allExported.Add(result);
                }
                Thread.Sleep(1);
            }
        });

        await Task.WhenAll(writerTasks);
        await exportTask;

        // Final export to capture any remaining metrics
        if (collector.TryExportMetricsAndReset(out var finalResult))
        {
            allExported.Add(finalResult);
        }

        long totalExportedCounterTotal = 0;
        long totalExportedHistogramCount = 0;
        foreach (var exported in allExported)
        {
            totalExportedCounterTotal += ExtractCounterTotal(exported);
            totalExportedHistogramCount += ExtractHistogramCount(exported);
        }

        _testOutputHelper.WriteLine($"Expected increments: {totalExpectedIncrements}");
        _testOutputHelper.WriteLine($"Total exported counter total: {totalExportedCounterTotal}");
        _testOutputHelper.WriteLine($"Total exported histogram count: {totalExportedHistogramCount}");
        _testOutputHelper.WriteLine($"Export rounds: {allExported.Count}");

        Assert.Equal(totalExpectedIncrements, totalExportedCounterTotal);
        Assert.Equal(totalExpectedIncrements, totalExportedHistogramCount);
    }

    /// <summary>
    /// Scenario B: Verifies that creating new entries (distinct node keys) concurrently
    /// with export doesn't lose data. Calls collector.IncrementMetrics directly to avoid
    /// interference from other tests that may clobber the static _collector.
    /// </summary>
    [Fact]
    public async Task NewEntriesDuringExport_NoMetricsLost()
    {
        using var collector = new AppTelemetryCollector();
        collector.Enable();

        const int nodeCount = 50;
        const int incrementsPerNode = 20_000;
        const int exportCount = 10;

        var allExported = new ConcurrentBag<string>();

        var writerTask = Task.Run(() =>
        {
            for (var n = 0; n < nodeCount; n++)
            {
                var nodeId = $"node{n}.example.com";
                var uuid = $"uuid-{n}";
                for (var j = 0; j < incrementsPerNode; j++)
                {
                    collector.IncrementMetrics(
                        TimeSpan.FromMilliseconds(10),
                        nodeId, null, uuid,
                        AppTelemetryServiceType.Query,
                        AppTelemetryCounterType.Total,
                        AppTelemetryRequestType.Query);
                }
            }
        });

        var exportTask = Task.Run(() =>
        {
            for (var e = 0; e < exportCount; e++)
            {
                if (collector.TryExportMetricsAndReset(out var result))
                {
                    allExported.Add(result);
                }
                Thread.Sleep(2);
            }
        });

        await writerTask;
        await exportTask;

        // Final export
        if (collector.TryExportMetricsAndReset(out var finalResult))
        {
            allExported.Add(finalResult);
        }

        long totalHistogramCount = 0;
        long totalCounterTotal = 0;
        foreach (var exported in allExported)
        {
            totalHistogramCount += ExtractAllHistogramCounts(exported);
            totalCounterTotal += ExtractAllCounterTotals(exported);
        }

        var expected = nodeCount * incrementsPerNode;
        _testOutputHelper.WriteLine($"Expected total: {expected}");
        _testOutputHelper.WriteLine($"Total histogram counts: {totalHistogramCount}");
        _testOutputHelper.WriteLine($"Total counter totals: {totalCounterTotal}");

        Assert.Equal(expected, totalHistogramCount);
        Assert.Equal(expected, totalCounterTotal);
    }

    /// <summary>
    /// Verifies that rapid enable/disable cycles don't cause data corruption or exceptions.
    /// </summary>
    [Fact]
    public async Task RapidEnableDisable_WithConcurrentWrites_NoExceptions()
    {
        using var collector = new AppTelemetryCollector();
        collector.Enable();

        const int cycles = 50;
        var cts = new CancellationTokenSource();

        var writerTask = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                collector.IncrementMetrics(
                    TimeSpan.FromMilliseconds(10),
                    Node, null, NodeUuid,
                    AppTelemetryServiceType.KeyValue,
                    AppTelemetryCounterType.Total,
                    AppTelemetryRequestType.KvRetrieval,
                    Bucket);
            }
        });

        for (var i = 0; i < cycles; i++)
        {
            collector.Disable();
            collector.Enable();
        }

        cts.Cancel();
        await writerTask;

        // Should not throw, and should be able to export cleanly after
        collector.TryExportMetricsAndReset(out _);
    }

    /// <summary>
    /// Verifies that multiple concurrent exports don't cause double-counting.
    /// Each metric should appear in exactly one export.
    /// </summary>
    [Fact]
    public async Task ConcurrentExports_NoDoubleCounting()
    {
        using var collector = new AppTelemetryCollector();
        collector.Enable();

        const int totalIncrements = 10_000;

        for (var i = 0; i < totalIncrements; i++)
        {
            collector.IncrementMetrics(
                TimeSpan.FromMilliseconds(50),
                Node, null, NodeUuid,
                AppTelemetryServiceType.Query,
                AppTelemetryCounterType.Total,
                AppTelemetryRequestType.Query);
        }

        var allExported = new ConcurrentBag<string>();
        var barrier = new Barrier(4);

        var exportTasks = new Task[4];
        for (var i = 0; i < 4; i++)
        {
            exportTasks[i] = Task.Run(() =>
            {
                barrier.SignalAndWait();
                if (collector.TryExportMetricsAndReset(out var result))
                {
                    allExported.Add(result);
                }
            });
        }
        await Task.WhenAll(exportTasks);

        // Final sweep
        if (collector.TryExportMetricsAndReset(out var finalResult))
        {
            allExported.Add(finalResult);
        }

        long totalCounterTotal = 0;
        long totalHistogramCount = 0;
        foreach (var exported in allExported)
        {
            totalCounterTotal += ExtractCounterTotal(exported);
            totalHistogramCount += ExtractHistogramCount(exported);
        }

        _testOutputHelper.WriteLine($"Export results count: {allExported.Count}");
        _testOutputHelper.WriteLine($"Total counter total: {totalCounterTotal}");
        _testOutputHelper.WriteLine($"Total histogram count: {totalHistogramCount}");

        Assert.Equal(totalIncrements, totalCounterTotal);
        Assert.Equal(totalIncrements, totalHistogramCount);
    }

    #region Helpers

    private static long ExtractCounterTotal(string exported)
    {
        var matches = Regex.Matches(exported, @"sdk_\w+_r_total\{[^}]+\}\s+(\d+)");
        return matches.Cast<Match>().Sum(m => long.Parse(m.Groups[1].Value));
    }

    private static long ExtractHistogramCount(string exported)
    {
        var matches = Regex.Matches(exported, @"_duration_milliseconds_count\{[^}]+\}\s+(\d+)");
        return matches.Cast<Match>().Sum(m => long.Parse(m.Groups[1].Value));
    }

    private static long ExtractAllHistogramCounts(string exported)
    {
        var matches = Regex.Matches(exported, @"_duration_milliseconds_count\{[^}]+\}\s+(\d+)");
        return matches.Cast<Match>().Sum(m => long.Parse(m.Groups[1].Value));
    }

    private static long ExtractAllCounterTotals(string exported)
    {
        var matches = Regex.Matches(exported, @"sdk_\w+_r_total\{[^}]+\}\s+(\d+)");
        return matches.Cast<Match>().Sum(m => long.Parse(m.Groups[1].Value));
    }

    #endregion
}
