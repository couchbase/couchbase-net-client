using System;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.Logging;
using Couchbase.N1QL;
using Couchbase.Search;
using Couchbase.Utils;
using Couchbase.Views;
using OpenTracing;
using OpenTracing.Tag;

namespace Couchbase.Tracing
{
    internal static class TracerExtensions
    {
        private const string Unknown = "Unknown";

        private static string GetOrGenerateOperationId(ISpan span)
        {
            if (span is Span s && s.Tags.TryGetValue(CouchbaseTags.OperationId, out var operationId))
            {
                return operationId.ToString();
            }

            return SequenceGenerator.GetNext().ToString();
        }

        #region KV

        private static string SanitizeTypeName(Type type)
        {
            const string tick = "`";
            return type.Name.Contains(tick)
                ? type.Name.Substring(0, type.Name.IndexOf(tick, StringComparison.OrdinalIgnoreCase))
                : type.Name;
        }

        internal static IScope StartParentScope(this ITracer tracer, IOperation operation, string bucketName = null,
            bool addIgnoreTag = false, bool ignoreActiveSpan = false)
        {
            var operationName = SanitizeTypeName(operation.GetType());
            var builder = tracer.BuildSpan(operation, operationName, bucketName);
            if (addIgnoreTag)
            {
                builder.WithIgnoreTag();
            }

            if (ignoreActiveSpan)
            {
                builder.IgnoreActiveSpan();
            }

            return builder.StartActive();
        }

        internal static IOperationResult GetResult(this IOperation operation, ITracer tracer, string bucketName)
        {
            using (tracer.BuildSpan(operation, CouchbaseOperationNames.ResponseDecoding, bucketName).StartActive())
            {
                return operation.GetResult();
            }
        }

        internal static IOperationResult<T> GetResultWithValue<T>(this IOperation<T> operation, ITracer tracer, string bucketName)
        {
            using (tracer.BuildSpan(operation, CouchbaseOperationNames.ResponseDecoding, bucketName).StartActive())
            {
                return operation.GetResultWithValue();
            }
        }

        internal static byte[] Write(this IOperation operation, ITracer tracer, string bucketName)
        {
            using (tracer.BuildSpan(operation, CouchbaseOperationNames.RequestEncoding, bucketName).StartActive())
            {
                return operation.Write();
            }
        }

        internal static async Task<byte[]> WriteAsync(this IOperation operation, ITracer tracer, string bucketName)
        {
            using (tracer.BuildSpan(operation, CouchbaseOperationNames.RequestEncoding, bucketName).StartActive())
            {
                return await operation.WriteAsync().ContinueOnAnyContext();
            }
        }

        internal static ISpanBuilder BuildSpan(this ITracer tracer, IOperation operation, IConnection connection, string bucketName = null)
        {
            return BuildSpan(tracer, operation, CouchbaseOperationNames.DispatchToServer, bucketName)
                .WithTag(Tags.PeerHostIpv4, connection.EndPoint?.ToString() ?? Unknown)
                .WithTag(CouchbaseTags.LocalAddress, connection.LocalEndPoint?.ToString() ?? Unknown)
                .WithTag(CouchbaseTags.LocalId, connection.ContextId);
        }

        private static ISpanBuilder BuildSpan(this ITracer tracer, IOperation operation, string operationName, string bucketName)
        {
            var builder = tracer.BuildSpan(operationName)
                .AddDefaultTags()
                .WithTag(CouchbaseTags.OperationId, $"0x{operation.Opaque:x}") // use opaque as hex value
                .WithTag(CouchbaseTags.Service, CouchbaseTags.ServiceKv)
                .WithTag(Tags.DbInstance, string.IsNullOrWhiteSpace(bucketName) ? Unknown : bucketName)
                .AsChildOf(tracer.ActiveSpan);

            // Only add document key if we're not redacting sensitive information
            if (LogManager.RedactionLevel == RedactionLevel.None)
            {
                builder.WithTag(CouchbaseTags.DocumentKey, operation.Key);
            }

            return builder;
        }

        #endregion

        #region View

        internal static IScope StartParentScope(this ITracer tracer, IViewQueryable query, bool addIgnoreTag = false)
        {
            var builder = tracer.BuildSpan(query);
            if (addIgnoreTag)
            {
                builder.WithIgnoreTag();
            }

            return builder.StartActive();
        }

        internal static ISpanBuilder BuildSpan(this ITracer tracer, IViewQueryable query)
        {
            const string operationName = "view";
            return tracer.BuildSpan(query, operationName);
        }

        internal static ISpanBuilder BuildSpan(this ITracer tracer, IViewQueryable query, string operationName)
        {
            return tracer.BuildSpan(operationName)
                .AddDefaultTags()
                .WithTag(CouchbaseTags.OperationId, GetOrGenerateOperationId(tracer.ActiveSpan))
                .WithTag(CouchbaseTags.ViewDesignDoc, query.DesignDocName)
                .WithTag(CouchbaseTags.ViewName, query.ViewName)
                .WithTag(CouchbaseTags.Service, CouchbaseTags.ServiceView)
                .AsChildOf(tracer.ActiveSpan);
        }

        #endregion

        #region N1QL

        internal static IScope StartParentScope(this ITracer tracer, IQueryRequest request, bool addIgnoreTag = false)
        {
            var builder = tracer.BuildSpan(request);
            if (addIgnoreTag)
            {
                builder.WithIgnoreTag();
            }

            return builder.StartActive();
        }

        internal static ISpanBuilder BuildSpan(this ITracer tracer, IQueryRequest query)
        {
            const string operationName = "n1ql";
            return tracer.BuildSpan(query, operationName);
        }

        internal static ISpanBuilder BuildSpan(this ITracer tracer, IQueryRequest query, string operationName)
        {
            return tracer.BuildSpan(operationName)
                .AddDefaultTags()
                .WithTag(CouchbaseTags.OperationId, query.CurrentContextId)
                .WithTag(CouchbaseTags.Service, CouchbaseTags.ServiceQuery)
                .WithTag(Tags.DbStatement, query.GetOriginalStatement())
                .AsChildOf(tracer.ActiveSpan);
        }

        #endregion

        #region FTS

        internal static IScope StartParentScope(this ITracer tracer, SearchQuery query, bool addIgnoreTag = false)
        {
            var builder = tracer.BuildSpan(query);
            if (addIgnoreTag)
            {
                builder.WithIgnoreTag();
            }

            return builder.StartActive();
        }

        internal static ISpanBuilder BuildSpan(this ITracer tracer, SearchQuery query)
        {
            const string operationName = "fts";
            return tracer.BuildSpan(query, operationName);
        }

        internal static ISpanBuilder BuildSpan(this ITracer tracer, SearchQuery query, string operationName)
        {
            return tracer.BuildSpan(operationName)
                .AddDefaultTags()
                .WithTag(CouchbaseTags.OperationId, GetOrGenerateOperationId(tracer.ActiveSpan))
                .WithTag(CouchbaseTags.Service, CouchbaseTags.ServiceSearch)
                .AsChildOf(tracer.ActiveSpan);
        }

        #endregion

        #region CBAS

        internal static IScope StartParentScope(this ITracer tracer, IAnalyticsRequest request, bool addIgnoreTag = false)
        {
            var builder = tracer.BuildSpan(request);
            if (addIgnoreTag)
            {
                builder.WithIgnoreTag();
            }

            return builder.StartActive();
        }

        internal static ISpanBuilder BuildSpan(this ITracer tracer, IAnalyticsRequest request)
        {
            const string operationName = "cbas";
            return tracer.BuildSpan(request, operationName);
        }

        internal static ISpanBuilder BuildSpan(this ITracer tracer, IAnalyticsRequest request, string operationName)
        {
            return tracer.BuildSpan(operationName)
                .AddDefaultTags()
                .WithTag(CouchbaseTags.OperationId, request.CurrentContextId)
                .WithTag(CouchbaseTags.Service, CouchbaseTags.ServiceAnalytics)
                .WithTag(Tags.DbStatement, request.OriginalStatement)
                .AsChildOf(tracer.ActiveSpan);
        }

        #endregion
    }
}

#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2018 Couchbase, Inc.
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

#endregion
