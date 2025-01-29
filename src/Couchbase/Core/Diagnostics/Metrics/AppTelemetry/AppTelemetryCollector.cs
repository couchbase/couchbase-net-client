using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using Couchbase.Core.DI;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
#nullable enable

internal class AppTelemetryCollector : IAppTelemetryCollector
{
    private bool _enabled;
    private ILogger<AppTelemetryCollector>? _logger;
    private IRedactor? _redactor;
    private WebSocketClientHandler? _webSocketClientHandler;
    private readonly object _enableLock = new();
    private readonly object _metricsLock = new();
    private readonly Uri? _endpoint;
    private CancellationTokenSource? _webSocketTokenSource;

    public ConcurrentDictionary<NodeAndBucket, AppTelemetryMetricSet> MetricSets { get; } = new();

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
        Username = clusterOptions.UserName;
        Password = clusterOptions.Password;
        _redactor = redactor;
        _logger = logger;
    }

    public void Initialize()
    {
        if (ClusterContext is not null)
        {
            _logger = ClusterContext.ServiceProvider.GetRequiredService<ILogger<AppTelemetryCollector>>();
        }

        if (_enabled)
        {
            _webSocketClientHandler = new WebSocketClientHandler(this);
            Enable();
        }
        else Disable();
    }

    public void Enable()
    {
        lock (_enableLock)
        {
            _enabled = true;
            _webSocketTokenSource = new CancellationTokenSource();
            _ = _webSocketClientHandler?.StartAsync(_webSocketTokenSource.Token);
        }
    }

    public void Disable()
    {
        lock (_enableLock)
        {
            _enabled = false;
            _webSocketTokenSource?.Cancel();
            MetricSets.Clear();
        }
    }

    public ClusterContext? ClusterContext { get; set; }
    public TimeSpan Backoff { get; set; }
    public TimeSpan PingInterval { get; set; }
    public TimeSpan PingTimeout { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    private bool? TlsEnabled => ClusterContext?.ClusterOptions.EffectiveEnableTls;

    /// <summary>
    /// Returns the Uri set from the ClusterOptions/ConnectionString,
    /// or the Uri constructed from the global BucketConfig (using appTelemetryPath from NodesExt).
    ///
    /// The Uri from the ClusterOptions takes precedence over the one from the BucketConfig.
    /// </summary>
    public Uri? Endpoint(int attempt) => _endpoint ?? ClusterContext?.GlobalConfig?.GetAppTelemetryPath(attempt, TlsEnabled);

    public void IncrementMetrics(TimeSpan? operationLatency, string node, string? alternateNode, string nodeUuid,
        AppTelemetryServiceType serviceType,
        AppTelemetryCounterType counterType,
        AppTelemetryRequestType? requestType = null,
        string? bucket = null)
    {
        if (!_enabled) return;
        if (string.IsNullOrEmpty(nodeUuid)) return; //Do not capture operations before a config is fetched
        if (!operationLatency.HasValue) return;

        requestType ??= AppTelemetryUtils.DetermineAppTelemetryRequestType(serviceType);

        //Only incrementing histograms for successful operations.
        //Timeouts and Cancellations histograms should only be incremented if an orphan is received, with its true latency.
        if (counterType == AppTelemetryCounterType.Total)
        {
            IncrementHistogram(requestType.Value, operationLatency, node, alternateNode, nodeUuid, bucket);
        }
        IncrementCounter(serviceType, counterType, node, alternateNode, nodeUuid, bucket);
    }

    public void IncrementHistogram(AppTelemetryRequestType name, TimeSpan? operationLatency, string node, string? alternateNode, string nodeUuid, string? bucket = null)
    {
        if (!_enabled) return;
        if (!operationLatency.HasValue) return;

        var targetKey = new NodeAndBucket(node, alternateNode, nodeUuid, bucket);

        AppTelemetryMetricSet metricSet;
        lock (_metricsLock)
        {
            // Ensures we don't add during a Clear
            metricSet = MetricSets.GetOrAdd(targetKey, _ => new AppTelemetryMetricSet());
        }

        metricSet.IncrementHistogram(name, operationLatency.Value);
    }

    public void IncrementCounter(AppTelemetryServiceType serviceType, AppTelemetryCounterType counterType, string node, string? alternateNode, string nodeUuid, string? bucket = null)
    {
        if (!_enabled) return;
        if (serviceType is AppTelemetryServiceType.KeyValue && bucket is null) return;

        var targetKey = new NodeAndBucket(node, alternateNode, nodeUuid, bucket);

        AppTelemetryMetricSet metricSet;
        lock (_metricsLock)
        {
            // Ensures we don't add during a Clear
            metricSet = MetricSets.GetOrAdd(targetKey, _ => new AppTelemetryMetricSet());
        }
        metricSet.IncrementCounter(serviceType, counterType);
    }

    public bool TryExportMetricsAndReset(out string metricsString)
    {
        metricsString = string.Empty;
        if (MetricSets.IsEmpty) return false;

        var sb = new StringBuilder();

        var snapshot = MetricSets.ToArray();

        lock (_metricsLock)
        {
            MetricSets.Clear();
        }

        foreach (var exported in snapshot.Select(entry => entry.Value.ExportAllMetrics(entry.Key)))
        {
            sb.Append(exported);
        }

        metricsString = sb.ToString();
        return true;
    }

    public LightweightStopwatch? StartNewLightweightStopwatch()
    {
        if (_enabled)
        {
            return LightweightStopwatch.StartNew();
        }
        return null;
    }

    public void Dispose()
    {
        _webSocketTokenSource?.Cancel();
        _webSocketClientHandler?.Dispose();
    }
}
