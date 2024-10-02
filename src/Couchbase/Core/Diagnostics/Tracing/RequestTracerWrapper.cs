namespace Couchbase.Core.Diagnostics.Tracing;

internal sealed class RequestTracerWrapper(IRequestTracer innerTracer, ClusterContext clusterContext) : IRequestTracer
{
    internal IRequestTracer InnerTracer { get; } = innerTracer;
    internal ClusterContext ClusterContext = clusterContext;
    internal ClusterLabels ClusterLabels => ClusterContext?.GlobalConfig?.ClusterLabels;

    public void Dispose()
    {
        InnerTracer.Dispose();
    }

    public IRequestSpan RequestSpan(string name, IRequestSpan parentSpan = null)
    {
        var span = new RequestSpanWrapper(InnerTracer.RequestSpan(name, parentSpan), ClusterLabels);
        span.SetClusterLabelsIfProvided(ClusterLabels);
        return span;
    }

    public IRequestTracer Start(TraceListener listener)
    {
        return InnerTracer.Start(listener);
    }
}
