namespace Couchbase.Core.Diagnostics.Tracing;

internal sealed class RequestTracerWrapper(IRequestTracer innerTracer, ClusterContext clusterContext) : IRequestTracer
{
    internal IRequestTracer InnerTracer { get; } = innerTracer;
    internal ClusterContext ClusterContext = clusterContext;
    internal ClusterLabels ClusterLabels => ClusterContext?.GlobalConfig?.ClusterLabels;

    internal ObservabilitySemanticConvention Convention { get; } = ResolveConvention(clusterContext);

    private static ObservabilitySemanticConvention ResolveConvention(ClusterContext clusterContext)
    {
        return clusterContext?.ClusterOptions?.ObservabilitySemanticConvention
            ?? ObservabilitySemanticConventionParser.FromEnvironment();
    }

    public void Dispose()
    {
        InnerTracer.Dispose();
    }

    public IRequestSpan RequestSpan(string name, IRequestSpan parentSpan = null)
    {
        var span = new RequestSpanWrapper(InnerTracer.RequestSpan(name, parentSpan), ClusterLabels, Convention);
        span.SetClusterLabelsIfProvided(ClusterLabels);
        return span;
    }

    public IRequestTracer Start(TraceListener listener)
    {
        return InnerTracer.Start(listener);
    }
}
