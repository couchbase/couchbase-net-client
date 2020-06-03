using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Core.Diagnostics.Tracing
{
    /// <summary>
    /// A tracer used by CouchbaseNetClient do trace internal operations.
    /// </summary>
    /// <remarks>Volatile.  (This interface may change in breaking ways during minor releases)</remarks>
    public interface IRequestTracer
    {
        /// <summary>
        /// Begin a span for an internal Couchbase operation.
        /// </summary>
        /// <param name="operationName">The name of the operation (should be a constant from the <c>RequestTracing</c> class).</param>
        /// <param name="parent">(Optional) The parent span.</param>
        /// <returns>A disposable that lets you end the span.</returns>
        IInternalSpan InternalSpan(string operationName, IRequestSpan parent);

        /// <summary>
        /// Begin a span wrapping a generic operation, allowing users of CouchbaseNetClient to wrap their own spans as the parent.
        /// </summary>
        /// <param name="operationName">The name of the operation (should be a constant from the <c>RequestTracing</c> class).</param>
        /// <param name="parent">(Optional) The parent span.</param>
        /// <returns>A disposable that lets you end the span.</returns>
        IRequestSpan RequestSpan(string operationName, IRequestSpan parent);
    }

    public class RequestTracing
    {
        public const string DispatchSpanName = "dispatch_to_server";
        public const string PayloadEncodingSpanName = "request_encoding";

        public class ServiceIdentifier
        {
            public const string Kv = "kv";
            public const string Query = "n1ql"; // the Java client has this as "query", but the RFC says "n1ql"
            public const string Search = "search";
            public const string View = "view";
            public const string Analytics = "analytics";
        }
    }
}
