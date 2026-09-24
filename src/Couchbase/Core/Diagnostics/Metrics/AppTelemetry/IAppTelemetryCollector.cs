using System;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Authentication.Authenticators;

namespace Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
#nullable enable

[InterfaceStability(Level.Volatile)]
internal interface IAppTelemetryCollector : IDisposable
{
    /// <summary>
    /// Increments metrics for a given operation. Called directly from the hot path
    /// via <see cref="MetricTracker.AppTelemetry.TrackOperation"/>.
    /// </summary>
    void IncrementMetrics(TimeSpan? operationLatency, string? node, string? alternateNode, string? nodeUuid,
        AppTelemetryServiceType serviceType,
        AppTelemetryCounterType counterType,
        AppTelemetryRequestType? requestType = null,
        string? bucket = null);

    void Initialize();

    /// <summary>
    /// Updates the App Telemetry endpoints from a global or bucket config.
    /// </summary>
    void OnConfigUpdated(BucketConfig config);

    /// <summary>
    /// Drops the App Telemetry endpoints of a config that is no longer tracked, such as a closed bucket.
    /// </summary>
    void OnConfigRemoved(string configName);

    void Disable();
    void Enable();

    bool TryExportMetricsAndReset(out string metricsString);

    ClusterContext? ClusterContext { get; set; }
    TimeSpan Backoff { get; set; }
    TimeSpan PingInterval { get; set; }
    TimeSpan PingTimeout { get; set; }
    IAuthenticator? Authenticator { get; set; }
}
