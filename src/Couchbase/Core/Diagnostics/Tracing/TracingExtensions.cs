using System;
using System.Net;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.Diagnostics.Tracing
{
    internal static class TracingExtensions
    {
        private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

        private static string? _dnsHostName;

        internal static long ToMicroseconds(this TimeSpan duration) =>
            duration.Ticks / TicksPerMicrosecond;

        internal static IInternalSpan WithDefaultAttributes(this IInternalSpan span) =>
            span.WithTag(CouchbaseTags.OpenTracingTags.Component, ClientIdentifier.GetClientDescription())
                .WithTag(CouchbaseTags.OpenTracingTags.DbType, CouchbaseTags.DbTypeCouchbase)
                .WithTag(CouchbaseTags.OpenTracingTags.SpanKind, CouchbaseTags.OpenTracingTags.SpanKindClient);

        internal static IInternalSpan OperationId(this IInternalSpan span, string operationId) =>
            span.WithTag(CouchbaseTags.OperationId, operationId);

        internal static IInternalSpan OperationId(this IInternalSpan span, OperationBase op) =>
            !span.IsNullSpan
                ? span.OperationId($"0x{op.Opaque:X}")
                : span;

        internal static IInternalSpan WithRemoteAddress(this IInternalSpan span, Uri remoteUri) =>
            !span.IsNullSpan
                ? span.WithTag(CouchbaseTags.RemoteAddress, $"{remoteUri.Host}:{remoteUri.Port}")
                : span;

        internal static IInternalSpan WithLocalAddress(this IInternalSpan span) =>
            !span.IsNullSpan
                ? span.WithTag(CouchbaseTags.LocalAddress, _dnsHostName ??= Dns.GetHostName())
                : span;

        internal static IInternalSpan RootSpan(this IRequestTracer tracer, string serviceName, string operation) =>
            tracer.InternalSpan(operation, null)
                .WithDefaultAttributes()
                .WithTag(CouchbaseTags.Service, serviceName);
    }
}
