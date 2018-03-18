using System;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.N1QL;
using Couchbase.Search;
using Couchbase.Views;
using OpenTracing;
using OpenTracing.NullTracer;

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

        internal static ISpan StartParentSpan(this ITracer tracer, IOperation operation, string bucketName = null, bool addIgnoreTag = false)
        {
            var operationName = SanitizeTypeName(operation.GetType());
            var builder = tracer.BuildSpan(operation, operationName, bucketName);
            if (addIgnoreTag)
            {
                builder.WithIgnoreTag();
            }

            var span = builder.Start();
            operation.ActiveSpan = span;
            return span;
        }

        internal static IOperationResult GetResult(this IOperation operation, ITracer tracer, string bucketName)
        {
            using (tracer
                .BuildSpan(operation, CouchbaseOperationNames.ResponseDecoding, bucketName)
                .Start())
            {
                return operation.GetResult();
            }
        }

        internal static IOperationResult<T> GetResultWithValue<T>(this IOperation<T> operation, ITracer tracer, string bucketName)
        {
            using (tracer
                .BuildSpan(operation, CouchbaseOperationNames.ResponseDecoding, bucketName)
                .Start())
            {
                return operation.GetResultWithValue();
            }
        }

        internal static byte[] Write(this IOperation operation, ITracer tracer, string bucketName)
        {
            using (tracer
                .BuildSpan(operation, CouchbaseOperationNames.RequestEncoding, bucketName)
                .Start())
            {
                return operation.Write();
            }
        }

        internal static async Task<byte[]> WriteAsync(this IOperation operation, ITracer tracer, string bucketName)
        {
            using (tracer
                .BuildSpan(operation, CouchbaseOperationNames.RequestEncoding, bucketName)
                .Start())
            {
                return await operation.WriteAsync();
            }
        }

        internal static ISpanBuilder BuildSpan(this ITracer tracer, IOperation operation, IConnection connection, string bucketName = null)
        {
            var span = BuildSpan(tracer, operation, CouchbaseOperationNames.DispatchToServer, bucketName);
            if (span is NullSpan)
            {
                return span;
            }

            return span
                .WithTag(Tags.PeerAddress, connection.EndPoint?.ToString() ?? Unknown)
                .WithTag(CouchbaseTags.LocalAddress, connection.LocalEndPoint?.ToString() ?? Unknown);
        }

        private static ISpanBuilder BuildSpan(this ITracer tracer, IOperation operation, string operationName, string bucketName)
        {
            var span = tracer.BuildSpan(operationName);
            if (span is NullSpan)
            {
                return span;
            }
            return span
                .WithTag(CouchbaseTags.OperationId, $"0x{operation.Opaque:x}") // use opaque as hex value
                .WithTag(CouchbaseTags.Service, CouchbaseTags.ServiceKv)
                .WithTag(Tags.DbInstance, string.IsNullOrWhiteSpace(bucketName) ? Unknown : bucketName)
                .AsChildOf(operation.ActiveSpan);
        }

        #endregion

        #region View

        internal static ISpan StartParentSpan(this ITracer tracer, IViewQueryable query, bool addIgnoreTag = false)
        {
            var builder = tracer.BuildSpan(query);
            if (addIgnoreTag)
            {
                builder.WithIgnoreTag();
            }

            var span = builder.Start();
            query.ActiveSpan = span;

            return span;
        }

        internal static ISpanBuilder BuildSpan(this ITracer tracer, IViewQueryable query)
        {
            const string operationName = "view";
            return tracer.BuildSpan(query, operationName);
        }

        internal static ISpanBuilder BuildSpan(this ITracer tracer, IViewQueryable query, string operationName)
        {
            var span = tracer.BuildSpan(operationName);
            if (span is NullSpan)
            {
                return span;
            }
            return span
                .WithTag(CouchbaseTags.OperationId, GetOrGenerateOperationId(query.ActiveSpan))
                .WithTag(CouchbaseTags.Service, CouchbaseTags.ServiceView)
                .AsChildOf(query.ActiveSpan);
        }

        #endregion

        #region N1QL

        internal static ISpan StartParentSpan(this ITracer tracer, IQueryRequest request, bool addIgnoreTag = false)
        {
            var builder = tracer.BuildSpan(request);
            if (addIgnoreTag)
            {
                builder.WithIgnoreTag();
            }

            var span = builder.Start();
            request.ActiveSpan = span;

            return span;
        }

        internal static ISpanBuilder BuildSpan(this ITracer tracer, IQueryRequest query)
        {
            const string operationName = "n1ql";
            return tracer.BuildSpan(query, operationName);
        }

        internal static ISpanBuilder BuildSpan(this ITracer tracer, IQueryRequest query, string operationName)
        {
            var span = tracer.BuildSpan(operationName);
            if (span is NullSpan)
            {
                return span;
            }
            return span
                .WithTag(CouchbaseTags.OperationId, query.CurrentContextId)
                .WithTag(CouchbaseTags.Service, CouchbaseTags.ServiceN1ql)
                .WithTag(Tags.DbStatement, query.GetOriginalStatement())
                .AsChildOf(query.ActiveSpan);
        }

        #endregion

        #region FTS

        internal static ISpan StartParentSpan(this ITracer tracer, SearchQuery query, bool addIgnoreTag = false)
        {
            var builder = tracer.BuildSpan(query);
            if (addIgnoreTag)
            {
                builder.WithIgnoreTag();
            }

            var span = builder.Start();
            query.ActiveSpan = span;

            return span;
        }

        internal static ISpanBuilder BuildSpan(this ITracer tracer, SearchQuery query)
        {
            const string operationName = "fts";
            return tracer.BuildSpan(query, operationName);
        }

        internal static ISpanBuilder BuildSpan(this ITracer tracer, SearchQuery query, string operationName)
        {
            var span = tracer.BuildSpan(operationName);
            if (span is NullSpan)
            {
                return span;
            }
            return span
                .WithTag(CouchbaseTags.OperationId, GetOrGenerateOperationId(query.ActiveSpan))
                .WithTag(CouchbaseTags.Service, CouchbaseTags.ServiceSearch)
                .AsChildOf(query.ActiveSpan);
        }

        #endregion

        #region CBAS

        internal static ISpan StartParentSpan(this ITracer tracer, IAnalyticsRequest request, bool addIgnoreTag = false)
        {
            var builder = tracer.BuildSpan(request);
            if (addIgnoreTag)
            {
                builder.WithIgnoreTag();
            }

            var span = builder.Start();
            request.ActiveSpan = span;

            return span;
        }

        internal static ISpanBuilder BuildSpan(this ITracer tracer, IAnalyticsRequest request)
        {
            const string operationName = "cbas";
            return tracer.BuildSpan(request, operationName);
        }

        internal static ISpanBuilder BuildSpan(this ITracer tracer, IAnalyticsRequest request, string operationName)
        {
            var span = tracer.BuildSpan(operationName);
            if (span is NullSpan)
            {
                return span;
            }
            return span
                .WithTag(CouchbaseTags.OperationId, request.CurrentContextId)
                .WithTag(CouchbaseTags.Service, CouchbaseTags.ServiceAnalytics)
                .WithTag(Tags.DbStatement, request.OriginalStatement)
                .AsChildOf(request.ActiveSpan);
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
