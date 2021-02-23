#nullable enable
using System;
using System.Net;
using Couchbase.Analytics;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Retry.Search;
using Couchbase.Query;
using Couchbase.Views;

namespace Couchbase.Core.Diagnostics.Tracing
{
    public static class RequestSpanExtensions
    {
        private static string? _dnsHostName;

        public static IRequestSpan WithCommonTags(this IRequestSpan span)
        {
            if (span.CanWrite)
            {
                span.SetAttribute(OuterRequestSpans.Attributes.System.Key, OuterRequestSpans.Attributes.System.Value);
                span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.NetTransport.Key,
                    InnerRequestSpans.DispatchSpan.Attributes.NetTransport.Value);
            }

            return span;
        }

        public static IRequestSpan RequestSpan(this IRequestTracer tracer, string serviceName, string operation)
        {
            var span = tracer.RequestSpan(serviceName);
            if (span.CanWrite)
            {
                span.SetAttribute(OuterRequestSpans.Attributes.Service, operation);
            }

            return span;
        }

        internal static IRequestSpan WithOperationId(this IRequestSpan span, IOperation operation)
        {
            if (span.CanWrite)
            {
                span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.OperationId, operation.Opaque);
            }

            return span;
        }

        internal static IRequestSpan WithOperationId(this IRequestSpan span, QueryOptions options)
        {
            if (span.CanWrite)
            {
                span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.OperationId, options.CurrentContextId ?? Guid.NewGuid().ToString());
            }

            return span;
        }

        internal static IRequestSpan WithOperationId(this IRequestSpan span, IAnalyticsRequest request)
        {
            if (span.CanWrite)
            {
                span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.OperationId, request.ClientContextId ?? Guid.NewGuid().ToString());
            }

            return span;
        }

        internal static IRequestSpan EncodingSpan(this IRequestSpan parentSpan)
        {
            var childSpan = parentSpan.ChildSpan(InnerRequestSpans.EncodingSpan.Name);
            if (childSpan.CanWrite)
            {
                childSpan.SetAttribute(InnerRequestSpans.EncodingSpan.Attributes.System.Key,
                    InnerRequestSpans.EncodingSpan.Attributes.System.Value);
            }

            return childSpan;
        }

        internal static IRequestSpan DispatchSpan(this IRequestSpan parentSpan)
        {
            var childSpan = parentSpan.ChildSpan(InnerRequestSpans.DispatchSpan.Name);
            if (childSpan.CanWrite)
            {
                childSpan.SetAttribute(InnerRequestSpans.EncodingSpan.Attributes.System.Key,
                    InnerRequestSpans.EncodingSpan.Attributes.System.Value);
                childSpan.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.NetTransport.Key,
                    InnerRequestSpans.DispatchSpan.Attributes.NetTransport.Value);
            }

            return childSpan;
        }

        internal static IRequestSpan DispatchSpan(this IRequestSpan parentSpan, SearchRequest request)
        {
            var childSpan = DispatchSpan(parentSpan);
            if (childSpan.CanWrite)
            {
                childSpan.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.OperationId, request.ClientContextId ?? Guid.NewGuid().ToString());
            }

            return childSpan;
        }

        internal static IRequestSpan DispatchSpan(this IRequestSpan parentSpan, QueryOptions options)
        {
            var childSpan = DispatchSpan(parentSpan);
            if (childSpan.CanWrite)
            {
                childSpan.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.OperationId, options.CurrentContextId ?? Guid.NewGuid().ToString());
            }

            return childSpan;
        }

        internal static IRequestSpan DispatchSpan(this IRequestSpan parentSpan, IAnalyticsRequest request)
        {
            var childSpan = DispatchSpan(parentSpan);
            if (childSpan.CanWrite)
            {
                childSpan.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.OperationId, request.ClientContextId ?? Guid.NewGuid().ToString());
            }

            return childSpan;
        }

        internal static IRequestSpan DispatchSpan(this IRequestSpan parentSpan, IViewQuery viewQuery)
        {
            var childSpan = DispatchSpan(parentSpan);
            if (childSpan.CanWrite)
            {
                childSpan.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.OperationId, viewQuery.ClientContextId ?? Guid.NewGuid().ToString());
            }

            return childSpan;
        }

        internal static IRequestSpan DispatchSpan(this IRequestSpan parentSpan, IOperation operation)
        {
            var childSpan = DispatchSpan(parentSpan);
            if (childSpan.CanWrite)
            {
                childSpan.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.OperationId,
                    operation.Opaque.ToString());
            }

            return childSpan;
        }

        internal static IRequestSpan WithRemoteAddress(this IRequestSpan span, Uri remoteUri)
        {
            if (span.CanWrite)
            {
                span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.RemoteHostname, remoteUri.Host);
                span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.RemotePort, $"{remoteUri.Port}");
            }

            return span;
        }

        internal static IRequestSpan WithLocalAddress(this IRequestSpan span)
        {
            if (span.CanWrite)
            {
                span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.LocalHostname, _dnsHostName ??= Dns.GetHostName());
            }

            return span;
        }

        internal static IRequestSpan WithStatement(this IRequestSpan span, string statement)
        {
            if (span.CanWrite)
            {
                span.SetAttribute(OuterRequestSpans.Attributes.Statement, statement);
            }

            return span;
        }

        internal static IRequestSpan WithOperation(this IRequestSpan span, string operation)
        {
            if (span.CanWrite)
            {
                span.SetAttribute(OuterRequestSpans.Attributes.Operation, operation);
            }

            return span;
        }

        internal static IRequestSpan WithOperation(this IRequestSpan span, IViewQuery viewQuery)
        {
            return span.WithOperation($"{viewQuery.DesignDocName}/{viewQuery.ViewName}");
        }
    }
}
