#if NETCOREAPP3_1_OR_GREATER
#nullable enable
using System.Collections.Generic;
using System.Diagnostics;

namespace Couchbase.Stellar.Core;

/// <summary>
/// Propagates the trace context of the gRPC client span on couchbase2 gRPC requests.
/// </summary>
/// <remarks>
/// RFC 77 sends the trace context to CNG so that its server span joins the SDK trace as a child
/// of the gRPC client span. When no listener asks for HttpClient spans, HttpClient still starts a
/// "System.Net.Http.HttpRequestOut" activity (with no ActivitySource) only to propagate the context,
/// and it sends the id of that activity. No tracer exports it, so CNG's span would have a parent that
/// is in no trace. This propagator skips such an activity and sends the id of its parent, the gRPC
/// client span. An HttpClient activity that a listener asked for comes from the "System.Net.Http"
/// ActivitySource, and it is sent as usual.
/// </remarks>
internal sealed class GrpcTraceContextPropagator(DistributedContextPropagator inner) : DistributedContextPropagator
{
    internal const string HttpClientActivityName = "System.Net.Http.HttpRequestOut";

    public override IReadOnlyCollection<string> Fields => inner.Fields;

    public override void Inject(Activity? activity, object? carrier, PropagatorSetterCallback? setter) =>
        inner.Inject(ActivityToPropagate(activity), carrier, setter);

    public override void ExtractTraceIdAndState(object? carrier, PropagatorGetterCallback? getter,
        out string? traceId, out string? traceState) =>
        inner.ExtractTraceIdAndState(carrier, getter, out traceId, out traceState);

    public override IEnumerable<KeyValuePair<string, string?>>? ExtractBaggage(object? carrier,
        PropagatorGetterCallback? getter) =>
        inner.ExtractBaggage(carrier, getter);

    internal static Activity? ActivityToPropagate(Activity? activity)
    {
        // Recorded does not identify the fallback activity: it copies the trace flags of its parent.
        while (activity is { Parent: not null, OperationName: HttpClientActivityName }
               && activity.Source.Name.Length == 0)
        {
            activity = activity.Parent;
        }

        return activity;
    }
}
#endif
