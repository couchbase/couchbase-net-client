using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Core.Diagnostics.Tracing;
using OpenTelemetry.Trace;

namespace Couchbase.Extensions.Tracing.OpenTelemetry
{
    public sealed class OpenTelemetryInternalSpan : IInternalSpan
    {
        private readonly Tracer _tracer;
        private readonly TelemetrySpan _span;

        internal TelemetrySpan Span => _span;

        public OpenTelemetryInternalSpan(Tracer tracer, TelemetrySpan span)
        {
            _tracer = tracer;
            _span = span;
        }

        public IInternalSpan StartPayloadEncoding()
        {
            var payloadEncodingSpan = _tracer.StartSpan(
                operationName: RequestTracing.PayloadEncodingSpanName,
                parent: _span,
                kind: SpanKind.Client);

            return new OpenTelemetryInternalSpan(_tracer, payloadEncodingSpan);
        }

        public IInternalSpan StartDispatch()
        {
            var dispatchSpan = _tracer.StartSpan(
                operationName: RequestTracing.DispatchSpanName,
                parent: _span,
                kind: SpanKind.Client);

            return new OpenTelemetryInternalSpan(_tracer, dispatchSpan);
        }

        public IInternalSpan SetAttribute(string key, string value)
        {
            _span.SetAttribute(key, value);
            return this;
        }

        public void Dispose() => Finish();

        public void Finish()
        {
            _span.End();
        }
    }
}
