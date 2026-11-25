namespace Couchbase.Core.Diagnostics.Tracing;

internal sealed class RequestTracerWrapper(IRequestTracer innerTracer, ClusterContext clusterContext) : IRequestTracer
{
    internal IRequestTracer InnerTracer { get; } = innerTracer;
    internal ClusterContext ClusterContext = clusterContext;
    internal ClusterLabels ClusterLabels => ClusterContext?.GlobalConfig?.ClusterLabels;

    internal ObservabilitySemanticConvention Convention { get; private set; } = ResolveConvention(clusterContext);

    /// <summary>
    /// Wraps <paramref name="innerTracer"/> with a fixed convention, for a cluster that has no
    /// <see cref="ClusterContext"/> (the Stellar cluster).
    /// </summary>
    internal RequestTracerWrapper(IRequestTracer innerTracer, ObservabilitySemanticConvention convention)
        : this(innerTracer, null)
    {
        Convention = convention;
    }

    /// <summary>
    /// Re-resolves the Convention from ClusterContext. Call once after ClusterContext is assigned.
    /// </summary>
    internal void ResolveAndSetConvention()
    {
        Convention = ResolveConvention(ClusterContext);
    }

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
