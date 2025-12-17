using System.ComponentModel;

namespace Couchbase.Core.Diagnostics.Metrics.AppTelemetry;

public enum AppTelemetryCounterType
{
    [Description("r_timedout")]
    TimedOut,
    [Description("r_canceled")]
    Canceled,
    [Description("r_total")]
    Total
}
