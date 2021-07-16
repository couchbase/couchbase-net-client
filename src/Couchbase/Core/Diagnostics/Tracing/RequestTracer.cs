using System.Diagnostics;

namespace Couchbase.Core.Diagnostics.Tracing
{
    /// <summary>
    /// An implementation of <see cref="IRequestTracer"/> that measures the duration of child spans
    /// and the total duration of the parent span - it is used to generate a report of the nth slowest
    /// requests which is useful for identifying slow operations.
    /// </summary>
    internal class RequestTracer : IRequestTracer
    {
        private static readonly ActivitySource ActivitySource = new("Couchbase.DotnetSdk.RequestTracer", "2.0.0");

        /// <inheritdoc />
        public IRequestSpan RequestSpan(string name, IRequestSpan parentSpan = null)
        {
            var activity = parentSpan == null ?
                ActivitySource.StartActivity(name) :
                ActivitySource.StartActivity(name, ActivityKind.Internal, parentSpan.Id!);

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
