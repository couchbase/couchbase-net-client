using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Couchbase.Core.Diagnostics.Tracing;
using OpenTelemetry.Trace;
using TraceListener = Couchbase.Core.Diagnostics.Tracing.TraceListener;

#nullable enable

namespace Couchbase.Extensions.Tracing.Otel.Tracing
{
    public class OpenTelemetryRequestTracer : IRequestTracer
    {
        internal const string SourceName = "Couchbase.DotnetSdk.OpenTelemetryRequestTracer";
        private static readonly ActivitySource ActivitySource = new(SourceName, "1.0.0");

        // Shared instance of a NoopRequestSpan which refers to this tracer and is a root span with no parent
        private readonly NoopRequestSpan _noopRootSpan;

        private readonly TracerProvider? _tracerProvider;

        public OpenTelemetryRequestTracer()
        {
            _noopRootSpan = new NoopRequestSpan(this);
        }

        [Obsolete("Use the parameterless constructor and AddCouchbaseInstrumentation() to your TracerProviderBuilder")]
        public OpenTelemetryRequestTracer(TracerProviderBuilder builder)
        {
            builder.AddSource(SourceName);
            _tracerProvider = builder.Build();
            _noopRootSpan = new NoopRequestSpan(this);
        }

        public void Dispose()
        {
            _tracerProvider?.Dispose();
        }

        public IRequestSpan RequestSpan(string name, IRequestSpan? parentSpan = null)
        {
            if (parentSpan is NoopRequestSpan noopSpan)
            {
                // Skip to the real parent above the NoopRequestSpan, if any.
                // Since we must check the type anyway, use the strongly-typed variable to get the parent so that it may be inlined.
                parentSpan = noopSpan.Parent;
            }

            var activity = parentSpan is OpenTelemetryRequestSpan openTelemetrySpan
                // It is faster to construct directly from the parent ActivityContext than from the parent ID
                ? ActivitySource.StartActivity(name, ActivityKind.Client, openTelemetrySpan.ActivityContext)
                : parentSpan?.Id == null ?
                    ActivitySource.StartActivity(name) :
                    ActivitySource.StartActivity(name, ActivityKind.Client, parentSpan.Id);

            if (activity == null)
            {
                // The activity source has no listeners or this trace is not being sampled
                return CreateNoopSpan(parentSpan);
            }

            return new OpenTelemetryRequestSpan(this, activity, parentSpan);
        }

        public IRequestTracer Start(TraceListener listener)
        {
           return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IRequestSpan CreateNoopSpan(IRequestSpan? parentSpan)
        {
            if (parentSpan == null)
            {
                // We're creating a root span, so reuse our shared root NoopRequestSpan
                return _noopRootSpan;
            }

            if (parentSpan is NoopRequestSpan)
            {
                // The parent is a NoopRequestSpan, so we can reuse it. The parent will be the last real activity
                // in the chain, subsequent children will keep referring to it.
                return parentSpan;
            }

            // We need to make a new NoopRequestSpan that refers to the parent
            return new NoopRequestSpan(this, parentSpan);
        }
    }
}
