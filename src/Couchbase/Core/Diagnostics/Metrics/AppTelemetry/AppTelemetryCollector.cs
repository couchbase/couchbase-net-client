using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using Couchbase.Core.Compatibility;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Authentication.Authenticators;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
#nullable enable

[InterfaceStability(Level.Volatile)]
internal class AppTelemetryCollector : IAppTelemetryCollector
{
    private volatile bool _enabled;
    private ILogger<AppTelemetryCollector>? _logger;
    private IRedactor? _redactor;
    private WebSocketClientHandler? _webSocketClientHandler;
    private readonly Uri? _endpoint;
    private CancellationTokenSource? _webSocketTokenSource;
    private ConcurrentDictionary<NodeAndBucket, AppTelemetryMetricSet> _metricSets = new();

    //Shim for unit tests
    internal ConcurrentDictionary<NodeAndBucket, AppTelemetryMetricSet> MetricSets => _metricSets;

    public AppTelemetryCollector()
    {
        _enabled = false;
    }

    public AppTelemetryCollector(ClusterContext clusterContext, IRedactor redactor, ILogger<AppTelemetryCollector> logger)
    {
        var clusterOptions = clusterContext.ClusterOptions;
        ClusterContext = clusterContext;
        _endpoint = clusterOptions.AppTelemetry.Endpoint;
        Backoff = clusterOptions.AppTelemetry.Backoff;
        PingInterval = clusterOptions.AppTelemetry.PingInterval;
        PingTimeout = clusterOptions.AppTelemetry.PingTimeout;
        _enabled = clusterOptions.AppTelemetry.Enabled;
        Authenticator = clusterOptions.GetEffectiveAuthenticator();
        _redactor = redactor;
        _logger = logger;
    }

    public void Initialize()
    {
        if (ClusterContext is not null)
        {
            _logger = ClusterContext.ServiceProvider.GetRequiredService<ILogger<AppTelemetryCollector>>();
        }

        if (_enabled && EndpointCount > 0)
        {
            _webSocketClientHandler = new WebSocketClientHandler(this);
            Enable();
        }
        else Disable();
    }

    public void Enable()
    {
        _enabled = true;
        MetricTracker.AppTelemetry.Register(this);
        _webSocketTokenSource = new CancellationTokenSource();
        _ = _webSocketClientHandler?.StartAsync(_webSocketTokenSource.Token);
    }

    public void Disable()
    {
        _enabled = false;
        MetricTracker.AppTelemetry.Unregister();
        _webSocketTokenSource?.Cancel();
        // Writers that already hold a metricSet reference will complete into the old dict, avoid orphaning in-flight writes.
        Interlocked.Exchange(ref _metricSets, new ConcurrentDictionary<NodeAndBucket, AppTelemetryMetricSet>());
    }

    public ClusterContext? ClusterContext { get; set; }
    public TimeSpan Backoff { get; set; }
    public TimeSpan PingInterval { get; set; }
    public TimeSpan PingTimeout { get; set; }
    public IAuthenticator? Authenticator { get; set; }
    private bool? TlsEnabled => ClusterContext?.ClusterOptions.EffectiveEnableTls;

    /// <summary>
    /// Returns the Uri set from the ClusterOptions/ConnectionString,
    /// or the Uri constructed from the global BucketConfig (using appTelemetryPath from NodesExt).
    ///
    /// The Uri from the ClusterOptions takes precedence over the one from the BucketConfig.
    /// </summary>
    public Uri? Endpoint(int attempt) => _endpoint ?? ClusterContext?.GlobalConfig?.GetAppTelemetryPath(attempt, TlsEnabled);

    public int EndpointCount => _endpoint != null ? 1 : ClusterContext?.GlobalConfig?.NodesWithAppTelemetry.Count ?? 0;

    public void IncrementMetrics(TimeSpan? operationLatency, string? node, string? alternateNode, string? nodeUuid,
        AppTelemetryServiceType serviceType,
        AppTelemetryCounterType counterType,
        AppTelemetryRequestType? requestType = null,
        string? bucket = null)
    {
        if (!_enabled) return;
        if (string.IsNullOrEmpty(nodeUuid)) return;

        requestType ??= AppTelemetryUtils.DetermineAppTelemetryRequestType(serviceType);

        var targetKey = new NodeAndBucket(node ?? string.Empty, alternateNode, nodeUuid, bucket);
        var dict = Volatile.Read(ref _metricSets);
        var metricSet = dict.GetOrAdd(targetKey, _ => new AppTelemetryMetricSet());

        if (counterType == AppTelemetryCounterType.Total && operationLatency.HasValue)
        {
            metricSet.IncrementHistogram(requestType.Value, operationLatency.Value);
        }

        // KV counters require a bucket
        if (serviceType == AppTelemetryServiceType.KeyValue && bucket == null) return;

        metricSet.IncrementCounter(serviceType, counterType);
    }

    public bool TryExportMetricsAndReset(out string metricsString)
    {
        metricsString = string.Empty;

        var dict = Volatile.Read(ref _metricSets);
        if (dict.IsEmpty) return false;

        var sb = new StringBuilder();
        foreach (var entry in dict)
        {
            var exported = entry.Value.ExportAllMetrics(entry.Key);
            if (!string.IsNullOrEmpty(exported))
            {
                sb.Append(exported);
            }
        }

        metricsString = sb.ToString();
        return metricsString.Length > 0;
    }

    public void Dispose()
    {
        MetricTracker.AppTelemetry.Unregister();
        _webSocketTokenSource?.Cancel();
        _webSocketClientHandler?.Dispose();
    }
}
