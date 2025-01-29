using System.ComponentModel;

namespace Couchbase.Core.Diagnostics.Metrics.AppTelemetry;

public enum AppTelemetryServiceType
{
    [Description("kv")]
    KeyValue = 0,

    [Description("query")]
    Query = 1,

    [Description("search")]
    Search = 2,

    [Description("analytics")]
    Analytics = 3,

    [Description("management")]
    Management = 4,

    [Description("eventing")]
    Eventing = 5,
}
