using System;
using System.Diagnostics;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Couchbase.Grpc.Protocol.Metrics;
using Google.Protobuf.WellKnownTypes;

namespace Couchbase.FitPerformer.Utils;

internal static class SystemMetricsReporter
{
    private static readonly Process CurrentProcess = Process.GetCurrentProcess();
    private static readonly Stopwatch _stopwatch = new Stopwatch();
    private static TimeSpan _lastProcessorTime;
    private static readonly TimeSpan Backoff = TimeSpan.FromSeconds(1);

    public static async Task StartReportingAsync(Channel<Couchbase.Grpc.Protocol.Run.Result> channel, Couchbase.Grpc.Protocol.Run.Config config = null, CancellationToken token = default)
    {
        if ((config?.StreamingConfig is null || !config.StreamingConfig.EnableMetrics)) return;

        Serilog.Log.Information("Starting System Metrics Reporter");

        _stopwatch.Start();
        _lastProcessorTime = CurrentProcess.TotalProcessorTime;

        while (!token.IsCancellationRequested)
        {
            await Task.Delay(Backoff, CancellationToken.None).ConfigureAwait(false);

            var metrics = System.Text.Json.JsonSerializer.Serialize(CaptureMetrics());

            await channel.Writer.WriteAsync(new Couchbase.Grpc.Protocol.Run.Result
            {
                Metrics = new Result
                {
                    Metrics = metrics,
                    Initiated = Timestamp.FromDateTime(DateTime.UtcNow),
                }
            }, CancellationToken.None).ConfigureAwait(false);
            Serilog.Log.Debug("System Metrics: {Metrics}", metrics);
        }
    }

    private static SystemMetrics CaptureMetrics()
    {
        var elapsedTime = _stopwatch.Elapsed;
        var currentProcessorTime = CurrentProcess.TotalProcessorTime;

        var cpuUsagePercent = CalculateCpuUsage(
            currentProcessorTime,
            _lastProcessorTime,
            elapsedTime);

        _stopwatch.Restart();
        _lastProcessorTime = currentProcessorTime;

        return new SystemMetrics
        {
            MemoryUsageMb = GC.GetTotalMemory(false) / (1024 * 1024),
            ProcessCpuPercent = cpuUsagePercent,
            ThreadCount = ThreadPool.ThreadCount
        };
    }

    private static double CalculateCpuUsage(
        TimeSpan currentProcessorTime,
        TimeSpan lastProcessorTime,
        TimeSpan elapsed)
    {
        var elapsedMs = elapsed.TotalMilliseconds;
        if (elapsedMs <= 0) return 0;

        var cpuUsedMs = (currentProcessorTime - lastProcessorTime).TotalMilliseconds;
        var cpuUsagePercent = (cpuUsedMs / (Environment.ProcessorCount * elapsedMs)) * 100;

        return Math.Round(Math.Min(100, Math.Max(0, cpuUsagePercent)), 2);
    }
}

internal record SystemMetrics
{
    [JsonPropertyName("memHeapUsedMB")] public double MemoryUsageMb { get; set; }

    [JsonPropertyName("processCpu")] public double ProcessCpuPercent { get; set; }

    [JsonPropertyName("threadCount")] public int ThreadCount { get; set; }
}