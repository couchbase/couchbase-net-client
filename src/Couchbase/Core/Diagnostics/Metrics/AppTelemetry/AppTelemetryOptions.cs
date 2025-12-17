using System;
using Couchbase.Core.Compatibility;

namespace Couchbase.Core.Diagnostics.Metrics.AppTelemetry;

#nullable enable

[InterfaceStability(Level.Volatile)]
public class AppTelemetryOptions
{
    /// <summary>
    /// Configures the endpoint where AppTelemetry data should be sent to.
    /// </summary>
    [InterfaceStability(Level.Volatile)]
    public Uri? Endpoint { get; set; }

    /// <summary>
    /// Enables/Disables AppTelemetry.
    /// <remarks>
    /// Enabled by default.
    /// </remarks>
    /// </summary>
    [InterfaceStability(Level.Volatile)]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Configures the maximum backoff for re-connecting to the AppTelemetry endpoint via WebSockets.
    /// Re-connecting to the endpoint is done using exponential backoff, up to this maximum value.
    /// <remarks>
    /// The default is 1h. This can also be configured via the connection string parameter "app_telemetry_backoff" in milliseconds.
    /// </remarks>
    /// </summary>
    [InterfaceStability(Level.Volatile)]
    public TimeSpan Backoff { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Configures the duration between consecutive PING commands sent to the server.
    /// <remarks>
    /// The default is 30s. This can also be configured via the connection string parameter "app_telemetry_ping_interval" in milliseconds.
    /// </remarks>
    /// </summary>
    [InterfaceStability(Level.Volatile)]
    public TimeSpan PingInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Configures the maximum timeout for the server to respond to a PING.
    /// <remarks>
    /// The default is 2s. This can also be configured via the connection string parameter "app_telemetry_ping_timeout" in milliseconds.
    /// </remarks>
    /// </summary>
    [InterfaceStability(Level.Volatile)]
    public TimeSpan PingTimeout { get; set; } = TimeSpan.FromSeconds(2);

}
