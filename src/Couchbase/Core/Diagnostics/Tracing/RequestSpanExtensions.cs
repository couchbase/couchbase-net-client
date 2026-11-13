#nullable enable
using System;
using System.Net;
using System.Runtime.CompilerServices;
using Couchbase.Analytics;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Retry.Search;
using Couchbase.Core.Utils;
using Couchbase.Management.Analytics;
using Couchbase.Management.Eventing;
using Couchbase.Query;
using Couchbase.Views;

namespace Couchbase.Core.Diagnostics.Tracing
{
    public static class RequestSpanExtensions
    {
        private static string? _dnsHostName;

        public static IRequestSpan LogOrphaned(this IRequestSpan span)
        {
            try
            {
                if (span.CanWrite)
                {
                    span.SetAttribute("orphaned", "true");
                }
            }
            catch
            {
                //ignore likely a duplicate attribute
            }

            return span;
        }

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

        internal static IRequestSpan WithOperationId(this IRequestSpan span, AnalyticsOptions request)
        {
            if (span.CanWrite)
            {
                span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.OperationId, request.ClientContextIdValue ?? Guid.NewGuid().ToString());
            }

            return span;
        }

        internal static IRequestSpan CompressionSpan(this IRequestSpan parentSpan)
        {
            var childSpan = parentSpan.ChildSpan(InnerRequestSpans.CompressionSpan.Name);
            if (childSpan.CanWrite)
            {
                childSpan.SetAttribute(InnerRequestSpans.CompressionSpan.Attributes.System.Key,
                    InnerRequestSpans.CompressionSpan.Attributes.System.Value);
            }

            return childSpan;
        }

        internal static IRequestSpan DecompressionSpan(this IRequestSpan parentSpan)
        {
            var childSpan = parentSpan.ChildSpan(InnerRequestSpans.DecompressionSpan.Name);
            if (childSpan.CanWrite)
            {
                childSpan.SetAttribute(InnerRequestSpans.DecompressionSpan.Attributes.System.Key,
                    InnerRequestSpans.DecompressionSpan.Attributes.System.Value);
            }

            return childSpan;
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

        internal static IRequestSpan DispatchSpan(this IRequestSpan parentSpan, FtsSearchRequest request)
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

        internal static IRequestSpan DispatchSpan(this IRequestSpan parentSpan, AnalyticsOptions request)
        {
            var childSpan = DispatchSpan(parentSpan);
            if (childSpan.CanWrite)
            {
                childSpan.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.OperationId, request.ClientContextIdValue ?? Guid.NewGuid().ToString());
            }

            return childSpan;
        }

        [Obsolete("The View service has been deprecated use the Query service instead.")]
        internal static IRequestSpan DispatchSpan(this IRequestSpan parentSpan, IViewQuery viewQuery)
        {
            var childSpan = DispatchSpan(parentSpan);
            if (childSpan.CanWrite)
            {
                childSpan.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.OperationId, viewQuery.ClientContextId ?? Guid.NewGuid().ToString());
            }

            return childSpan;
        }

        internal static IRequestSpan DispatchSpan(this IRequestSpan parentSpan, FunctionOptionsBase eventingFunctionOptions)
        {
            var childSpan = DispatchSpan(parentSpan);
            if (childSpan.CanWrite)
            {
                childSpan.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.OperationId, eventingFunctionOptions.ClientContextId ?? Guid.NewGuid().ToString());
            }

            return childSpan;
        }

        internal static IRequestSpan DispatchSpan(this IRequestSpan parentSpan, IOperation operation)
        {
            var childSpan = DispatchSpan(parentSpan);
            if (childSpan.CanWrite)
            {
                childSpan.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.OperationId,
                    operation.Opaque.ToStringInvariant());
            }

            return childSpan;
        }

        internal static IRequestSpan WithRemoteAddress(this IRequestSpan span, Uri remoteUri)
        {
            if (span.CanWrite)
            {
                span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.RemoteHostname, remoteUri.DnsSafeHost);
                span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.RemotePort, $"{remoteUri.Port}");
            }

            return span;
        }

        /// <summary>
        /// Adds the address and port that an HTTP request was sent to (RFC 67: network.peer.address
        /// and network.peer.port). This is the host of the request URI, without the brackets of an IPv6
        /// address. When the URI holds a hostname,
        /// HttpClient resolves it, and the SDK does not see the IP address that it connects to.
        /// </summary>
        internal static IRequestSpan WithPeerAddress(this IRequestSpan span, Uri remoteUri)
        {
            if (span.CanWrite)
            {
                span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.NetworkPeerAddress, remoteUri.DnsSafeHost);
                span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.NetworkPeerPort, remoteUri.Port.ToStringInvariant());
            }

            return span;
        }

        internal static IRequestSpan WithLocalId(this IRequestSpan span, string? localId)
        {
            if (span.CanWrite)
            {
                span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.LocalId, localId ?? Guid.NewGuid().ToString());
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

        internal static IRequestSpan WithLocalPort(this IRequestSpan span, uint localPort)
        {
            if (span.CanWrite)
            {
                span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.LocalPort, localPort);
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

        /// <summary>
        /// Adds a N1QL statement to the span. RFC 67: the stable <c>db.query.text</c> is set only when
        /// <paramref name="hasParameters"/> is true, because without parameters the statement can contain
        /// sensitive data. The legacy <c>db.statement</c> is always set, so existing users keep it.
        /// </summary>
        internal static IRequestSpan WithStatement(this IRequestSpan span, string statement, bool hasParameters)
        {
            if (!span.CanWrite)
            {
                return span;
            }

            // A span that is not wrapped has no semantic convention mapping, so it only ever sets the legacy name.
            if (!hasParameters && span is RequestSpanWrapper wrapper)
            {
                wrapper.SetLegacyAttribute(OuterRequestSpans.Attributes.Statement, statement);
            }
            else
            {
                span.SetAttribute(OuterRequestSpans.Attributes.Statement, statement);
            }

            return span;
        }

        internal static IRequestSpan WithService(this IRequestSpan span, string service)
        {
            if (span.CanWrite)
            {
                span.SetAttribute(OuterRequestSpans.Attributes.Service, service);
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

        [Obsolete("The View service has been deprecated use the Query service instead.")]
        internal static IRequestSpan WithOperation(this IRequestSpan span, IViewQuery viewQuery)
        {
            return span.WithOperation($"{viewQuery.DesignDocName}/{viewQuery.ViewName}");
        }

        internal static IRequestSpan SetClusterLabelsIfProvided(this IRequestSpan span, ClusterLabels? clusterLabels)
        {
            if (clusterLabels?.ClusterUuid is not null)
            {
                span.SetAttribute(OuterRequestSpans.Attributes.ClusterUuid, clusterLabels.ClusterUuid);
            }
            if (clusterLabels?.ClusterName is not null)
            {
                span.SetAttribute(OuterRequestSpans.Attributes.ClusterName, clusterLabels.ClusterName);
            }

            return span;
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
