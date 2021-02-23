namespace Couchbase.Core.Diagnostics.Tracing
{
    /// <summary>
    /// A NOOP implementation of <see cref="IRequestTracer"/> used when tracing is disabled.
    /// </summary>
    public class NoopRequestTracer : IRequestTracer
    {
        public static IRequestTracer Instance = new NoopRequestTracer();

        public IRequestSpan RequestSpan(string name, IRequestSpan parentSpan = null)
        {
            return NoopRequestSpan.Instance;
        }

        public IRequestTracer Start(TraceListener listener)
        {
            return this;
        }

        public void Dispose()
        {
        }
    }
}
