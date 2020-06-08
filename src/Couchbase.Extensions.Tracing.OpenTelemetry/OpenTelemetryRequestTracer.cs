using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Core.Diagnostics.Tracing;
using OpenTelemetry.Trace;

namespace Couchbase.Extensions.Tracing.OpenTelemetry
{
    public class OpenTelemetryRequestTracer : IRequestTracer
    {
        private readonly Tracer _tracer;

        public OpenTelemetryRequestTracer(Tracer tracer)
        {
            _tracer = tracer;
        }

        public IInternalSpan InternalSpan(string operationName, IRequestSpan parent)
        {
            var parentInternalSpan = parent as OpenTelemetryInternalSpan;
            var span = parentInternalSpan == null
                ? _tracer.StartRootSpan(operationName, SpanKind.Client)
                : _tracer.StartSpan(operationName, parentInternalSpan.Span, SpanKind.Client);
            return new OpenTelemetryInternalSpan(_tracer, span);
        }

        public IRequestSpan RequestSpan(string operationName, IRequestSpan parent) =>
            InternalSpan(operationName, parent);
    }
}
