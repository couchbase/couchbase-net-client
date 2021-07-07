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
