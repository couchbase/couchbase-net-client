using System.Diagnostics;
using System.Runtime.CompilerServices;

#nullable enable

namespace Couchbase.Core.Diagnostics.Tracing
{
    /// <summary>
    /// An implementation of <see cref="IRequestTracer"/> that measures the duration of child spans
    /// and the total duration of the parent span - it is used to generate a report of the nth slowest
    /// requests which is useful for identifying slow operations.
    /// </summary>
    internal class RequestTracer : IRequestTracer
    {
        internal const string ActivitySourceName = "Couchbase.DotnetSdk.RequestTracer";
        private static readonly ActivitySource ActivitySource = new(ActivitySourceName, "2.0.0");

        // Shared instance of a NoopRequestSpan which refers to this tracer and is a root span with no parent
        private readonly NoopRequestSpan _noopRootSpan;

        /// <summary>
        /// Creates a new RequestTracer.
        /// </summary>
        public RequestTracer()
        {
            _noopRootSpan = new(this);
        }

        /// <inheritdoc />
        public IRequestSpan RequestSpan(string name, IRequestSpan? parentSpan = null)
        {
            if (parentSpan is NoopRequestSpan noopSpan)
            {
                // Skip to the real parent above the NoopRequestSpan, if any.
                // Since we must check the type anyway, use the strongly-typed variable to get the parent so that it may be inlined.
                parentSpan = noopSpan.Parent;
            }

            var activity = parentSpan is RequestSpan requestSpan
                // It is faster to construct directly from the parent ActivityContext than from the parent ID
                ? ActivitySource.StartActivity(name, ActivityKind.Client, requestSpan.ActivityContext)
                : parentSpan?.Id == null ?
                    ActivitySource.StartActivity(name) :
                    ActivitySource.StartActivity(name, ActivityKind.Client, parentSpan.Id);

            if (activity == null)
            {
                // The activity source has no listeners or this trace is not being sampled
                return CreateNoopSpan(parentSpan);
            }

            var span = new RequestSpan(this, activity, parentSpan);
            if (parentSpan == null)
            {
                span.WithCommonTags();
            }

            return span;
        }

        /// <inheritdoc />
        public IRequestTracer Start(TraceListener listener)
        {
            ActivitySource.AddActivityListener(listener.Listener);
            return this;
        }

        public void Dispose()
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IRequestSpan CreateNoopSpan(IRequestSpan? parentSpan)
        {
            if (parentSpan == null)
            {
                // We're creating a root span, so reuse our shared root NoopRequestSpan
                return _noopRootSpan;
            }

            if (parentSpan is NoopRequestSpan)
            {
                // The parent is a NoopRequestSpan, so we can reuse it. The parent will be the last real activity
                // in the chain, subsequent children will keep referring to it.
                return parentSpan;
            }

            // We need to make a new NoopRequestSpan that refers to the parent
            return new NoopRequestSpan(this, parentSpan);
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
