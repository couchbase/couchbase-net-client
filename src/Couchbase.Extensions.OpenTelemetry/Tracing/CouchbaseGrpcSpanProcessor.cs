using System.Diagnostics;
using OpenTelemetry;

namespace Couchbase.Extensions.Tracing.Otel.Tracing
{
    /// <summary>
    /// Names the gRPC client spans of couchbase2 operations after the gRPC method, as RFC 77 requires.
    /// </summary>
    /// <remarks>
    /// Grpc.Net.Client starts a client span named "Grpc.Net.Client.GrpcOut" for each call. RFC 77 uses
    /// that span in place of "dispatch_to_server", named after the method
    /// (for example "couchbase.query.v1.QueryService/Query"). This processor renames only spans whose
    /// parent is a Couchbase span. It does not change or drop gRPC spans from other code in the
    /// application.
    /// </remarks>
    internal sealed class CouchbaseGrpcSpanProcessor : BaseProcessor<Activity>
    {
        /// <summary>
        /// The name of the ActivitySource of Grpc.Net.Client.
        /// </summary>
        internal const string GrpcSourceName = "Grpc.Net.Client";

        private const string GrpcMethodTag = "grpc.method";

        public override void OnStart(Activity data)
        {
            if (data.Source.Name != GrpcSourceName
                || data.Parent?.Source.Name != OpenTelemetryRequestTracer.SourceName)
            {
                return;
            }

            // Grpc.Net.Client sets grpc.method to "/package.Service/Method" when the span starts.
            if (data.GetTagItem(GrpcMethodTag) is string method && method.Length > 1)
            {
                data.DisplayName = method[0] == '/' ? method.Substring(1) : method;
            }
        }
    }
}
