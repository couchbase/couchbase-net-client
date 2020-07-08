using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;

namespace Couchbase.Core.Diagnostics.Tracing
{
    internal static class TracingExtensions
    {
        private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

        internal static long ToMicroseconds(this TimeSpan duration)
        {
            return duration.Ticks / TicksPerMicrosecond;
        }

        internal static IInternalSpan WithDefaultAttributes(this IInternalSpan span)
        {
            span.SetAttribute(CouchbaseTags.OpenTracingTags.Component, ClientIdentifier.GetClientDescription());
            span.SetAttribute(CouchbaseTags.OpenTracingTags.DbType, CouchbaseTags.DbTypeCouchbase);
            span.SetAttribute(CouchbaseTags.OpenTracingTags.SpanKind, CouchbaseTags.OpenTracingTags.SpanKindClient);
            return span;
        }

        internal static IInternalSpan OperationId(this IInternalSpan span, string operationId) =>
            span.SetAttribute(CouchbaseTags.OperationId, operationId);

        internal static IInternalSpan OperationId(this IInternalSpan span, OperationBase op) =>
            span.OperationId(op.Opaque.ToString(CultureInfo.InvariantCulture));

        internal static IInternalSpan RootSpan(this IRequestTracer tracer, string serviceName, string operation) =>
            tracer.InternalSpan(operation, null)
                .WithDefaultAttributes()
                .SetAttribute(CouchbaseTags.Service, serviceName);

    }
}
