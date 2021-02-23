using System;

namespace Couchbase.Core.Diagnostics.Tracing
{
    /// <summary>
    /// The abstraction for tracing in the SDK.
    /// </summary>
    /// <remarks>
    /// Multiple implementation exists, internal within the SDK and as packages for 3rd parties
    /// (OpenTelemetry, OpenTracing, etc.). It is recommended that one of these packages be used
    /// for writing your own implementation.
    /// </remarks>
    public interface IRequestTracer : IDisposable
    {
        /// <summary>
        /// Creates a new request span with or without a parent span.
        /// </summary>
        /// <param name="name">The name of the top-level operation (i.e. "cb.get")</param>
        /// <param name="parentSpan">A parent span, otherwise null.</param>
        /// <returns>A request span that wraps the actual tracer implementation span.</returns>
        IRequestSpan RequestSpan(string name, IRequestSpan parentSpan = null);

        /// <summary>
        /// Starts tracing given a <see cref="TraceListener"/> implementation.
        /// </summary>
        /// <param name="listener"></param>
        /// <returns></returns>
        IRequestTracer Start(TraceListener listener);
    }
}
