using System;
using System.Diagnostics;
using Couchbase.Core.Diagnostics.Tracing;
using OpenTelemetry.Trace;
using TraceListener = Couchbase.Core.Diagnostics.Tracing.TraceListener;

#nullable enable

namespace Couchbase.Extensions.Tracing.Otel.Tracing
{
    public class OpenTelemetryRequestTracer : IRequestTracer
    {
        internal static readonly string SourceName = "Couchbase.DotnetSdk.OpenTelemetryRequestTracer";
        private static readonly ActivitySource ActivitySource = new(SourceName, "1.0.0");

        private readonly TracerProvider? _tracerProvider;

        public OpenTelemetryRequestTracer()
        {
        }

        [Obsolete("Use the parameterless constructor and AddCouchbaseInstrumentation() to your TracerProviderBuilder")]
        public OpenTelemetryRequestTracer(TracerProviderBuilder builder)
        {
            builder.AddSource(SourceName);
            _tracerProvider = builder.Build();
        }

        public void Dispose()
        {
            _tracerProvider?.Dispose();
        }

        public IRequestSpan RequestSpan(string name, IRequestSpan? parentSpan = null)
        {
            var activity = parentSpan?.Id == null ?
                ActivitySource.StartActivity(name) :
                ActivitySource.StartActivity(name, ActivityKind.Internal, parentSpan.Id);

            return new OpenTelemetryRequestSpan(this, activity, parentSpan);
        }

        public IRequestTracer Start(TraceListener listener)
        {
           return this;
        }
    }
}
