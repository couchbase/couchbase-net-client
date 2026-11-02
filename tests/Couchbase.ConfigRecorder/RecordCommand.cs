using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Couchbase.ConfigRecorder.DnsSrv;
using Couchbase.ConfigRecorder.Load;
using Couchbase.ConfigRecorder.Probe;
using Couchbase.ConfigRecorder.Sdk;
using Couchbase.ConfigRecorder.Tap;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Couchbase.ConfigRecorder;

internal static class RecordCommand
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StatusInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(15);

    public static async Task<int> RunAsync(RecorderSettings settings)
    {
        using var stop = new CancellationTokenSource();
        ConsoleCancelEventHandler onCancel = (_, e) =>
        {
            if (stop.IsCancellationRequested) return;
            e.Cancel = true;
            Console.WriteLine("Stopping. Press Ctrl+C again to quit at once.");
            stop.Cancel();
        };
        Console.CancelKeyPress += onCancel;
        using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            stop.Cancel();
        });

        var recorder = Recorder.Create(settings.OutputFolder);
        Console.WriteLine($"Recording to {recorder.RunFolder}");
        recorder.Write("recorder", "start", new JsonObject
        {
            ["settings"] = settings.ToSafeJson(),
            ["sdkVersion"] = typeof(Cluster).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            ["runtime"] = RuntimeInformation.FrameworkDescription,
            ["os"] = RuntimeInformation.OSDescription,
            ["runFolder"] = recorder.RunFolder,
        });

        var tap = new TapHub(recorder);
        var sdkLogs = FileLoggerProvider.CreateFactory(Path.Combine(recorder.RunFolder, "sdk.log"), LogLevel.Debug, SdkLogFilter.Keep);
        var probeLogs = FileLoggerProvider.CreateFactory(Path.Combine(recorder.RunFolder, "probe-sdk.log"), LogLevel.Information);
        var dns = new DnsSrvWatcher(recorder, settings.ConnectionString, TimeSpan.FromMilliseconds(settings.DnsSrvIntervalMs));

        ICluster? cluster = null;
        SdkObserver? observer = null;
        ConfigProbe? probe = null;
        LoadGenerator? load = null;
        var exitCode = 0;
        var stopReason = "stop requested";
        try
        {
            dns.Start();

            ClusterOptions? options = null;
            while (cluster is null)
            {
                stop.Token.ThrowIfCancellationRequested();
                try
                {
                    options = CreateOptions(settings, sdkLogs);
                    TappingConnectionFactory.Register(options, tap, TapHub.SdkRole);
                    cluster = await Cluster.ConnectAsync(options, stop.Token).ConfigureAwait(false);
                    recorder.Write("sdk", "connected");
                }
                catch (Exception e) when (!stop.IsCancellationRequested)
                {
                    recorder.Error("sdk", "Could not connect. Retrying in 5 s.", e, alwaysConsole: true);
                    await Task.Delay(RetryDelay, stop.Token).ConfigureAwait(false);
                }
            }

            var bucket = await OpenBucketAsync(recorder, cluster, settings.Bucket, stop.Token).ConfigureAwait(false);
            recorder.Write("sdk", "bucket-open", new JsonObject
            {
                ["bucket"] = bucket.Name,
                ["networkResolution"] = options!.EffectiveNetworkResolution,
            });

            // Each part starts on its own, so one that fails does not stop the others.
            observer = TryStart(recorder, "sdk", () =>
            {
                var bucketBase = bucket as BucketBase
                    ?? throw new InvalidOperationException($"The bucket is a {bucket.GetType().Name}, which the recorder cannot observe.");
                var o = new SdkObserver(recorder, bucketBase, TimeSpan.FromMilliseconds(settings.SdkSampleIntervalMs));
                o.Start();
                return o;
            });

            probe = TryStart(recorder, "probe", () =>
            {
                var p = new ConfigProbe(recorder, tap, CreateProbeOptions(settings, probeLogs, tap, options), settings.Bucket,
                    TimeSpan.FromMilliseconds(settings.ProbeIntervalMs),
                    options.ConnectionStringValue!.GetBootstrapEndpoints(options.EffectiveEnableTls));
                p.Start();
                return p;
            });

            if (settings.Load.Workers > 0)
            {
                load = TryStart(recorder, "load", () =>
                {
                    var l = new LoadGenerator(recorder, tap, bucket.DefaultCollection(), settings.Load);
                    l.Start();
                    return l;
                });
            }
            else
            {
                recorder.Write("load", "off");
            }

            Console.WriteLine("Recording. Press Ctrl+C to stop.");
            await StatusLoopAsync(recorder, tap, observer, probe, load, stop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            recorder.Error("recorder", "The recorder failed.", e);
            stopReason = "error";
            exitCode = 1;
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
            Console.WriteLine("Flushing.");
            if (load is not null) await StopAsync(recorder, "load", load.DisposeAsync).ConfigureAwait(false);
            if (probe is not null) await StopAsync(recorder, "probe", probe.DisposeAsync).ConfigureAwait(false);
            if (observer is not null) await StopAsync(recorder, "sdk", observer.DisposeAsync).ConfigureAwait(false);
            if (cluster is not null) await StopAsync(recorder, "sdk", cluster.DisposeAsync).ConfigureAwait(false);
            await StopAsync(recorder, "dns", dns.DisposeAsync).ConfigureAwait(false);
            sdkLogs.Dispose();
            probeLogs.Dispose();
            await StopAsync(recorder, "tap", tap.DisposeAsync).ConfigureAwait(false);
            recorder.Write("recorder", "stop", new JsonObject
            {
                ["reason"] = stopReason,
                ["events"] = recorder.EventCount + 1,
            });
            await recorder.DisposeAsync().ConfigureAwait(false);
            Console.WriteLine($"Done. Run folder {recorder.RunFolder}");
        }

        return exitCode;
    }

    private static async Task<IBucket> OpenBucketAsync(Recorder recorder, ICluster cluster, string name, CancellationToken token)
    {
        IBucket? bucket = null;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                bucket ??= await cluster.BucketAsync(name).ConfigureAwait(false);
                await bucket.WaitUntilReadyAsync(TimeSpan.FromSeconds(30),
                    new WaitUntilReadyOptions().ServiceTypes(ServiceType.KeyValue).CancellationToken(token)).ConfigureAwait(false);
                return bucket;
            }
            catch (Exception e) when (!token.IsCancellationRequested)
            {
                recorder.Error("sdk", $"Bucket {name} is not ready. Retrying in 5 s.", e, alwaysConsole: true);
                await Task.Delay(RetryDelay, token).ConfigureAwait(false);
            }
        }
    }

    private static ClusterOptions CreateOptions(RecorderSettings settings, ILoggerFactory loggerFactory)
    {
        var options = new ClusterOptions()
            .WithConnectionString(settings.ConnectionString)
            .WithPasswordAuthentication(settings.Username, settings.Password)
            .WithLogging(loggerFactory);

        if (settings.AcceptAnyServerCertificate)
        {
            options.TlsSettings.KvCertificateValidationCallback = (_, _, _, _) => true;
            options.TlsSettings.HttpCertificateValidationCallback = (_, _, _, _) => true;
        }

        return options;
    }

    /// <summary>
    /// Options for the probe context. One connection per probe node, no polling, no telemetry, and the
    /// configs that its nodes publish are dropped.
    /// </summary>
    private static ClusterOptions CreateProbeOptions(RecorderSettings settings, ILoggerFactory loggerFactory, TapHub tap,
        ClusterOptions sdkOptions)
    {
        var options = CreateOptions(settings, loggerFactory);
        options.NumKvConnections = 1;
        options.MaxKvConnections = 1;
        options.EnableConfigPolling = false;
        options.AppTelemetry.Enabled = false;
        options.WithLoggingMeterOptions(o => o.Enabled(false));
        options.EffectiveNetworkResolution = sdkOptions.EffectiveNetworkResolution;
        options.AddClusterService<IConfigHandler, NullConfigHandler>();
        TappingConnectionFactory.Register(options, tap, TapHub.ProbeRole);
        return options;
    }

    private static T? TryStart<T>(Recorder recorder, string component, Func<T> start) where T : class
    {
        try
        {
            return start();
        }
        catch (Exception e)
        {
            recorder.Error(component, "Could not start. The other parts keep recording.", e, alwaysConsole: true);
            return null;
        }
    }

    private static async Task StatusLoopAsync(Recorder recorder, TapHub tap, SdkObserver? observer, ConfigProbe? probe,
        LoadGenerator? load, CancellationToken token)
    {
        var started = Stopwatch.GetTimestamp();
        using var timer = new PeriodicTimer(StatusInterval);
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
        {
            try
            {
                var (up, total) = probe?.Health() ?? (0, 0);
                var elapsed = Stopwatch.GetElapsedTime(started);
                var loadText = load is null
                    ? "load off"
                    : $"ops/s {load.LastOpsPerSecond}  errors {load.LastErrors}  nmvb {load.LastNotMyVBucket}";
                Console.WriteLine(
                    $"[{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}] {loadText}  " +
                    $"sdk bucket {observer?.LatestBucketVersion ?? "-"}  probe {up}/{total} up  " +
                    $"conns sdk {tap.SdkConnections} probe {tap.ProbeConnections}  events {recorder.EventCount}");
            }
            catch (Exception e)
            {
                recorder.Error("recorder", "Could not print the status line.", e);
            }
        }
    }

    private static async Task StopAsync(Recorder recorder, string component, Func<ValueTask> dispose)
    {
        try
        {
            var task = dispose().AsTask();
            if (await Task.WhenAny(task, Task.Delay(StopTimeout)).ConfigureAwait(false) != task)
            {
                recorder.Error(component, $"Stopping took more than {StopTimeout.TotalSeconds} s. Moving on.");
            }
            else
            {
                await task.ConfigureAwait(false);
            }
        }
        catch (Exception e)
        {
            recorder.Error(component, "Stopping failed.", e);
        }
    }
}
