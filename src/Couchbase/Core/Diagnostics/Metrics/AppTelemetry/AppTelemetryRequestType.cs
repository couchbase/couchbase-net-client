using System.ComponentModel;
using Couchbase.Core.Compatibility;

namespace Couchbase.Core.Diagnostics.Metrics.AppTelemetry;

[InterfaceStability(Level.Volatile)]
public enum AppTelemetryRequestType
{
    [Description("sdk_kv_retrieval")]
    KvRetrieval,
    [Description("sdk_kv_mutation_nondurable")]
    KvMutationNonDurable,
    [Description("sdk_kv_mutation_durable")]
    KvMutationDurable,
    [Description("sdk_query")]
    Query,
    [Description("sdk_search")]
    Search,
    [Description("sdk_analytics")]
    Analytics,
    [Description("sdk_management")]
    Management,
    [Description("sdk_eventing")]
    Eventing
}
