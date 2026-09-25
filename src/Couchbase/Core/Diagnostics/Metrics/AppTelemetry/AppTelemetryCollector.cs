using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Authentication.Authenticators;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
#nullable enable

[InterfaceStability(Level.Volatile)]
internal class AppTelemetryCollector : IAppTelemetryCollector
{
    // AppTelemetryOptions.Enabled. When false, App Telemetry stays off for the life of the cluster.
    private readonly bool _enabledInOptions;
    // The only flag read per operation. Equals _enabled && !_paused.
    private volatile bool _collecting;
    private ILogger<AppTelemetryCollector>? _logger;
    private IRedactor? _redactor;
    private WebSocketClientHandler? _webSocketClientHandler;
    private readonly Uri? _endpoint;
    private CancellationTokenSource? _webSocketTokenSource;
    private ConcurrentDictionary<NodeAndBucket, AppTelemetryMetricSet> _metricSets = new();

    // Guards the fields below. Never taken on the metrics hot path.
    private readonly object _remotesLock = new();
    private bool _enabled;
    // Set when no node advertises an App Telemetry path, so nothing would ever read the metrics.
    private bool _paused;
    private readonly Dictionary<string, (ConfigVersion Version, IReadOnlyList<Uri> Uris)> _configUris = new();
    private IReadOnlyList<Uri> _remotes = Array.Empty<Uri>();

    //Shim for unit tests
    internal ConcurrentDictionary<NodeAndBucket, AppTelemetryMetricSet> MetricSets => _metricSets;
    internal WebSocketClientHandler? WebSocketClientHandler => _webSocketClientHandler;
    internal bool IsPaused
    {
        get { lock (_remotesLock) return _paused; }
    }

    public AppTelemetryCollector()
    {
    }

    public AppTelemetryCollector(ClusterContext clusterContext, IRedactor redactor, ILogger<AppTelemetryCollector> logger)
    {
        var clusterOptions = clusterContext.ClusterOptions;
        ClusterContext = clusterContext;
        _endpoint = clusterOptions.AppTelemetry.Endpoint;
        Backoff = clusterOptions.AppTelemetry.Backoff;
        PingInterval = clusterOptions.AppTelemetry.PingInterval;
        PingTimeout = clusterOptions.AppTelemetry.PingTimeout;
        _enabledInOptions = clusterOptions.AppTelemetry.Enabled;
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

        if (!_enabledInOptions)
        {
            Disable();
            return;
        }

        lock (_remotesLock)
        {
            // Bootstrap can be retried, so only the first call starts the reporter.
            if (_webSocketClientHandler is not null) return;

            _webSocketClientHandler = new WebSocketClientHandler(this);
            if (_endpoint is not null) _remotes = [_endpoint];
            PublishRemotesLocked();
        }

        Enable();
    }

    public void OnConfigUpdated(BucketConfig config)
    {
        if (!_enabledInOptions || _endpoint is not null) return;

        lock (_remotesLock)
        {
            try
            {
                if (TrackConfigLocked(config))
                {
                    PublishRemotesLocked();
                }
            }
            catch (Exception e)
            {
                _logger?.LogDebug(e, "App Telemetry could not process config revision {revision}.", config.Rev);
            }
        }
    }

    public void OnConfigRemoved(string configName)
    {
        if (!_enabledInOptions || _endpoint is not null || configName is null) return;

        lock (_remotesLock)
        {
            if (_configUris.Remove(configName) && RecomputeRemotesLocked())
            {
                PublishRemotesLocked();
            }
        }
    }

    /// <summary>
    /// Stores the URIs of a config newer than the one already tracked under its name.
    /// Returns true if the overall endpoint set changed.
    /// </summary>
    private bool TrackConfigLocked(BucketConfig config)
    {
        var name = config.Name ?? BucketConfig.GlobalBucketName;

        // Polling republishes the same revision from every node, so skip those before building URIs.
        if (!config.IgnoreRev && _configUris.TryGetValue(name, out var tracked)
                              && config.ConfigVersion <= tracked.Version)
        {
            return false;
        }

        // Configs from the HTTP rebootstrap path keep the default resolution, so prefer the one chosen at bootstrap.
        var networkResolution = ClusterContext?.ClusterOptions.EffectiveNetworkResolution ?? config.NetworkResolution;
        var uris = config.GetAppTelemetryUris(TlsEnabled, networkResolution);

        _configUris[name] = (config.ConfigVersion, uris);
        return RecomputeRemotesLocked();
    }

    private bool RecomputeRemotesLocked()
    {
        var remotes = _configUris.Values
            .SelectMany(p => p.Uris)
            .Distinct()
            .OrderBy(p => p.OriginalString, StringComparer.Ordinal)
            .ToList();

        if (remotes.SequenceEqual(_remotes)) return false;

        _remotes = remotes;
        if (_logger?.IsEnabled(LogLevel.Debug) == true)
        {
            var endpoints = string.Join(", ", remotes);
            _logger.LogDebug("App Telemetry endpoints from cluster configs: [{Endpoints}]",
                _redactor?.SystemData(endpoints) ?? endpoints);
        }
        return true;
    }

    private void PublishRemotesLocked()
    {
        // Before Initialize the set is only stored. Initialize pushes it to the handler.
        if (_webSocketClientHandler is null) return;

        _webSocketClientHandler.UpdateRemotes(_remotes);
        SetPausedLocked(_remotes.Count == 0);
    }

    private void SetPausedLocked(bool paused)
    {
        if (_paused == paused) return;
        _paused = paused;
        UpdateCollectingLocked();

        if (paused)
        {
            _logger?.LogDebug("No node advertises an App Telemetry endpoint. Pausing App Telemetry collection.");
            ResetMetricSets();
        }
        else
        {
            _logger?.LogDebug("App Telemetry endpoints are available. Resuming App Telemetry collection.");
        }
    }

    private void UpdateCollectingLocked() => _collecting = _enabled && !_paused;

    public void Enable()
    {
        lock (_remotesLock)
        {
            _enabled = true;
            UpdateCollectingLocked();
        }
        MetricTracker.AppTelemetry.Register(this);
        _webSocketTokenSource = new CancellationTokenSource();
        _ = _webSocketClientHandler?.StartAsync(_webSocketTokenSource.Token);
    }

    public void Disable()
    {
        lock (_remotesLock)
        {
            _enabled = false;
            UpdateCollectingLocked();
        }
        MetricTracker.AppTelemetry.Unregister();
        _webSocketTokenSource?.Cancel();
        // Writers that already hold a metricSet reference will complete into the old dict, avoid orphaning in-flight writes.
        ResetMetricSets();
    }

    private void ResetMetricSets() => Interlocked.Exchange(ref _metricSets, new());

    public ClusterContext? ClusterContext { get; set; }
    public TimeSpan Backoff { get; set; }
    public TimeSpan PingInterval { get; set; }
    public TimeSpan PingTimeout { get; set; }
    public IAuthenticator? Authenticator { get; set; }
    private bool TlsEnabled => ClusterContext?.ClusterOptions.EffectiveEnableTls ?? false;

    public void IncrementMetrics(TimeSpan? operationLatency, string? node, string? alternateNode, string? nodeUuid,
        AppTelemetryServiceType serviceType,
        AppTelemetryCounterType counterType,
        AppTelemetryRequestType? requestType = null,
        string? bucket = null)
    {
        // Read the dictionary before the flag. Pausing clears the flag before it swaps the dictionary,
        // so a write that passes the check lands in a dictionary that the pause discards.
        var dict = Volatile.Read(ref _metricSets);
        if (!_collecting) return;
        if (string.IsNullOrEmpty(nodeUuid)) return;

        requestType ??= AppTelemetryUtils.DetermineAppTelemetryRequestType(serviceType);

        var targetKey = new NodeAndBucket(node ?? string.Empty, alternateNode, nodeUuid, bucket);
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
