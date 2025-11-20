using System;
using Couchbase.Core.Compatibility;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Authentication.Authenticators;
using Couchbase.Utils;

namespace Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
#nullable enable

[InterfaceStability(Level.Volatile)]
internal interface IAppTelemetryCollector : IDisposable
{
    public void IncrementMetrics(TimeSpan? operationLatency, string node, string? alternateNode, string nodeUuid,
        AppTelemetryServiceType serviceType,
        AppTelemetryCounterType counterType,
        AppTelemetryRequestType? requestType = null,
        string? bucket = null);

    void IncrementCounter(AppTelemetryServiceType serviceType, AppTelemetryCounterType counterType, string node, string? alternateNode, string nodeUuid, string? bucket = null);

    void IncrementHistogram(AppTelemetryRequestType name, TimeSpan? operationLatency, string node, string? alternateNode, string nodeUuid,
        string? bucket = null);

    void Initialize();

    void Disable();
    void Enable();

    LightweightStopwatch? StartNewLightweightStopwatch();

    bool TryExportMetricsAndReset(out string metricsString);

    ClusterContext? ClusterContext { get; set; }
    TimeSpan Backoff { get; set; }
    TimeSpan PingInterval { get; set; }
    TimeSpan PingTimeout { get; set; }
    Uri? Endpoint(int attempt);
    IAuthenticator? Authenticator { get; set; }
}
