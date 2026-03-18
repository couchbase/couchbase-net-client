using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Metrics.AppTelemetry;

namespace Couchbase.LoadTests.Core.Diagnostics.Metrics;

/// <summary>
/// Benchmarks locking strategies for AppTelemetryCollector during concurrent
/// increments with periodic exports (single exporter thread).
///
/// Three strategies are compared:
///   - Monitor lock: exclusive lock for both writers and exporter (dictionary swap on export)
///   - ReaderWriterLockSlim: read lock for writers, write lock for exporter (dictionary swap on export)
///   - Lock-free (current implementation): ConcurrentDictionary + Interlocked ops with no external lock
///
/// Smaller ExportIntervalMs stresses writer/exporter contention, hiher ExportIntervalMs measures
/// pure increment throughput with less export interference.
///
/// Lost metrics (counters and histograms) are reported as extra columns, to watch out for logical race conditions
/// </summary>
[MemoryDiagnoser]
[Config(typeof(LostMetricsConfig))]
[InProcess]
public class AppTelemetryLockingBenchmark
{
    private static readonly ConcurrentDictionary<string, (long LostCounters, long LostHistograms)> LostMetrics = new();

    private NodeAndBucket[] _keys;

    [Params(10, 20)]
    public int WriterCount { get; set; }

    [Params(500_000)]
    public int IncrementsPerWriter { get; set; }

    [Params(10, 500)]
    public int ExportIntervalMs { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _keys = new NodeAndBucket[WriterCount];
        for (var i = 0; i < WriterCount; i++)
        {
            _keys[i] = new NodeAndBucket($"node{i}.example.com", null, $"uuid-{i}", "travel-sample");
        }
    }

    [Benchmark(Baseline = true)]
    public long MonitorLock()
    {
        var harness = new MonitorLockHarness();
        return RunHarness(harness, nameof(MonitorLock));
    }

    [Benchmark]
    public long ReaderWriterLock()
    {
        var harness = new RwLockHarness();
        return RunHarness(harness, nameof(ReaderWriterLock));
    }

    [Benchmark]
    public long LockFree()
    {
        var harness = new LockFreeHarness();
        return RunHarness(harness, nameof(LockFree));
    }

    private long RunHarness(ILockHarness harness, string methodName)
    {
        var exportedCounters = new ConcurrentBag<long>();
        var exportedHistograms = new ConcurrentBag<long>();
        var barrier = new Barrier(WriterCount + 1);
        using var cts = new CancellationTokenSource();

        var writerTasks = new Task[WriterCount];
        for (var i = 0; i < WriterCount; i++)
        {
            var key = _keys[i];
            writerTasks[i] = Task.Factory.StartNew(() =>
            {
                barrier.SignalAndWait();
                for (var j = 0; j < IncrementsPerWriter; j++)
                {
                    harness.Increment(key);
                }
            }, TaskCreationOptions.LongRunning);
        }

        var exportTask = Task.Factory.StartNew(() =>
        {
            barrier.SignalAndWait();
            while (!cts.Token.WaitHandle.WaitOne(ExportIntervalMs))
            {
                var (counters, histograms) = harness.ExportAndReset();
                if (counters > 0) exportedCounters.Add(counters);
                if (histograms > 0) exportedHistograms.Add(histograms);
            }
        }, TaskCreationOptions.LongRunning);

        Task.WaitAll(writerTasks);
        cts.Cancel();
        exportTask.Wait();

        var (remainingCounters, remainingHistograms) = harness.ExportAndReset();
        if (remainingCounters > 0) exportedCounters.Add(remainingCounters);
        if (remainingHistograms > 0) exportedHistograms.Add(remainingHistograms);

        var totalCounters = exportedCounters.Sum();

        var totalHistograms = exportedHistograms.Aggregate(0, (current, h) => (int)(current + h));

        var expected = (long)WriterCount * IncrementsPerWriter;
        var lostCounters = expected - totalCounters;
        var lostHistograms = expected - totalHistograms;

        var caseKey = $"{methodName}|{WriterCount}|{IncrementsPerWriter}|{ExportIntervalMs}";
        LostMetrics[caseKey] = (lostCounters, lostHistograms);

        return totalCounters;
    }

    #region Lock strategy abstractions

    private interface ILockHarness
    {
        void Increment(NodeAndBucket key);
        (long CounterTotal, long HistogramCount) ExportAndReset();
    }

    /// <summary>
    /// Uses Monitor (lock) for both writers and the exporter. All threads serialize.
    /// Models the old approach: dictionary swap under exclusive lock on export.
    /// </summary>
    private sealed class MonitorLockHarness : ILockHarness
    {
        private readonly object _lock = new();
        private ConcurrentDictionary<NodeAndBucket, AppTelemetryMetricSet> _metricSets = new();

        public void Increment(NodeAndBucket key)
        {
            lock (_lock)
            {
                var metricSet = _metricSets.GetOrAdd(key, _ => new AppTelemetryMetricSet());
                metricSet.IncrementHistogram(AppTelemetryRequestType.KvRetrieval, TimeSpan.FromMilliseconds(50));
                metricSet.IncrementCounter(AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.Total);
            }
        }

        public (long CounterTotal, long HistogramCount) ExportAndReset()
        {
            ConcurrentDictionary<NodeAndBucket, AppTelemetryMetricSet> old;
            lock (_lock)
            {
                if (_metricSets.IsEmpty) return (0, 0);
                old = _metricSets;
                _metricSets = new ConcurrentDictionary<NodeAndBucket, AppTelemetryMetricSet>();
            }

            long counterTotal = 0;
            long histogramCount = 0;
            foreach (var entry in old)
            {
                var exported = entry.Value.ExportAllMetrics(entry.Key);
                counterTotal += ExtractCounterTotal(exported);
                histogramCount += ExtractHistogramCount(exported);
            }
            return (counterTotal, histogramCount);
        }
    }

    /// <summary>
    /// Uses ReaderWriterLockSlim. Writers take a read lock (concurrent),
    /// exporter takes a write lock (exclusive). Dictionary swap on export.
    /// </summary>
    private sealed class RwLockHarness : ILockHarness
    {
        private readonly ReaderWriterLockSlim _lock = new();
        private ConcurrentDictionary<NodeAndBucket, AppTelemetryMetricSet> _metricSets = new();

        public void Increment(NodeAndBucket key)
        {
            _lock.EnterReadLock();
            try
            {
                var metricSet = _metricSets.GetOrAdd(key, _ => new AppTelemetryMetricSet());
                metricSet.IncrementHistogram(AppTelemetryRequestType.KvRetrieval, TimeSpan.FromMilliseconds(50));
                metricSet.IncrementCounter(AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.Total);
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public (long CounterTotal, long HistogramCount) ExportAndReset()
        {
            ConcurrentDictionary<NodeAndBucket, AppTelemetryMetricSet> old;
            _lock.EnterWriteLock();
            try
            {
                if (_metricSets.IsEmpty) return (0, 0);
                old = _metricSets;
                _metricSets = new ConcurrentDictionary<NodeAndBucket, AppTelemetryMetricSet>();
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            long counterTotal = 0;
            long histogramCount = 0;
            foreach (var entry in old)
            {
                var exported = entry.Value.ExportAllMetrics(entry.Key);
                counterTotal += ExtractCounterTotal(exported);
                histogramCount += ExtractHistogramCount(exported);
            }
            return (counterTotal, histogramCount);
        }
    }

    /// <summary>
    /// Lock-free approach matching the current AppTelemetryCollector implementation.
    /// No external locks; ConcurrentDictionary.GetOrAdd for metric set lookup, Interlocked
    /// operations for all value mutations. Export iterates the same dictionary and each
    /// value's SnapshotAndReset atomically captures and clears its fields.
    /// </summary>
    private sealed class LockFreeHarness : ILockHarness
    {
        private readonly ConcurrentDictionary<NodeAndBucket, AppTelemetryMetricSet> _metricSets = new();

        public void Increment(NodeAndBucket key)
        {
            var metricSet = _metricSets.GetOrAdd(key, _ => new AppTelemetryMetricSet());
            metricSet.IncrementHistogram(AppTelemetryRequestType.KvRetrieval, TimeSpan.FromMilliseconds(50));
            metricSet.IncrementCounter(AppTelemetryServiceType.KeyValue, AppTelemetryCounterType.Total);
        }

        public (long CounterTotal, long HistogramCount) ExportAndReset()
        {
            long counterTotal = 0;
            long histogramCount = 0;
            foreach (var entry in _metricSets)
            {
                var exported = entry.Value.ExportAllMetrics(entry.Key);
                counterTotal += ExtractCounterTotal(exported);
                histogramCount += ExtractHistogramCount(exported);
            }
            return (counterTotal, histogramCount);
        }
    }

    #endregion

    private static long ExtractCounterTotal(string exported)
    {
        var matches = Regex.Matches(exported, @"sdk_\w+_r_total\{[^}]+\}\s+(\d+)");
        long total = 0;
        foreach (Match m in matches)
        {
            total += long.Parse(m.Groups[1].Value);
        }
        return total;
    }

    private static long ExtractHistogramCount(string exported)
    {
        var matches = Regex.Matches(exported, @"_duration_milliseconds_count\{[^}]+\}\s+(\d+)");
        long total = 0;
        foreach (Match m in matches)
        {
            total += long.Parse(m.Groups[1].Value);
        }
        return total;
    }

    #region Custom BDN columns for lost metrics

    private sealed class LostMetricsConfig : ManualConfig
    {
        public LostMetricsConfig()
        {
            AddColumn(new LostMetricsColumn("LostCounters", r => r.LostCounters));
            AddColumn(new LostMetricsColumn("LostHistograms", r => r.LostHistograms));
        }
    }

    private sealed class LostMetricsColumn(string name, Func<(long LostCounters, long LostHistograms), long> selector)
        : IColumn
    {
        public string Id { get; } = name;
        public string ColumnName { get; } = name;
        public bool AlwaysShow => true;
        public ColumnCategory Category => ColumnCategory.Custom;
        public int PriorityInCategory => 0;
        public bool IsNumeric => true;
        public UnitType UnitType => UnitType.Dimensionless;
        public string Legend => $"Number of {ColumnName} lost during the benchmark run";

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
        {
            return GetValue(summary, benchmarkCase, SummaryStyle.Default);
        }

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
        {
            var descriptor = benchmarkCase.Descriptor;
            var methodName = descriptor.WorkloadMethod.Name;

            var writerCount = 0;
            var incrementsPerWriter = 0;
            var exportIntervalMs = 0;
            foreach (var p in benchmarkCase.Parameters.Items)
            {
                switch (p.Name)
                {
                    case nameof(WriterCount): writerCount = (int)p.Value; break;
                    case nameof(IncrementsPerWriter): incrementsPerWriter = (int)p.Value; break;
                    case nameof(ExportIntervalMs): exportIntervalMs = (int)p.Value; break;
                }
            }

            var caseKey = $"{methodName}|{writerCount}|{incrementsPerWriter}|{exportIntervalMs}";

            return LostMetrics.TryGetValue(caseKey, out var lost) ? selector(lost).ToString() : "N/A";
        }

        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;
        public bool IsAvailable(Summary summary) => true;
    }

    #endregion
}
