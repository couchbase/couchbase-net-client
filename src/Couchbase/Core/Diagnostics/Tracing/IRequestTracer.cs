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
