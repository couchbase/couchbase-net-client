using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
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
            span.WithTag(CouchbaseTags.OpenTracingTags.Component, ClientIdentifier.GetClientDescription());
            span.WithTag(CouchbaseTags.OpenTracingTags.DbType, CouchbaseTags.DbTypeCouchbase);
            span.WithTag(CouchbaseTags.OpenTracingTags.SpanKind, CouchbaseTags.OpenTracingTags.SpanKindClient);
            return span;
        }

        internal static IInternalSpan OperationId(this IInternalSpan span, string operationId) =>
            span.WithTag(CouchbaseTags.OperationId, operationId);

        internal static IInternalSpan OperationId(this IInternalSpan span, OperationBase op) =>
            span.OperationId($"0x{op.Opaque:X}");

        internal static IInternalSpan WithRemoteAddress(this IInternalSpan span, Uri remoteUri) =>
            span.WithTag(CouchbaseTags.RemoteAddress, $"{remoteUri.Host}:{remoteUri.Port}");

        internal static IInternalSpan WithLocalAddress(this IInternalSpan span) =>
            span.WithTag(CouchbaseTags.LocalAddress, Dns.GetHostName());

        internal static IInternalSpan RootSpan(this IRequestTracer tracer, string serviceName, string operation) =>
            tracer.InternalSpan(operation, null)
                .WithDefaultAttributes()
                .WithTag(CouchbaseTags.Service, serviceName);

    }
}
