using System;

namespace Couchbase.Core.Diagnostics.Tracing;

internal sealed class RequestSpanWrapper(IRequestSpan innerSpan, ClusterLabels clusterLabels = null, ObservabilitySemanticConvention convention = ObservabilitySemanticConvention.Legacy) : IRequestSpan
{
    internal ClusterLabels ClusterLabels => clusterLabels;
    internal ObservabilitySemanticConvention ObservabilitySemanticConvention => convention;

    public void Dispose()
    {
        innerSpan.Dispose();
    }

    public IRequestSpan SetAttribute(string key, bool value)
    {
        SemanticConventionEmitter.EmitAttribute(ObservabilitySemanticConvention, key, value, innerSpan,
            static (span, k, v) => span.SetAttribute(k, v));
        return this;
    }

    public IRequestSpan SetAttribute(string key, string value)
    {
        SemanticConventionEmitter.EmitAttribute(ObservabilitySemanticConvention, key, value, innerSpan,
            static (span, k, v) => span.SetAttribute(k, v));
        return this;
    }

    public IRequestSpan SetAttribute(string key, uint value)
    {
        SemanticConventionEmitter.EmitAttribute(ObservabilitySemanticConvention, key, value, innerSpan,
            static (span, k, v) => span.SetAttribute(k, v));
        return this;
    }

    public IRequestSpan AddEvent(string name, DateTimeOffset? timestamp = null)
    {
        return innerSpan.AddEvent(name, timestamp);
    }

    public void End()
    {
        innerSpan.End();
    }

    public IRequestSpan Parent
    {
        get => innerSpan.Parent;
        set => innerSpan.Parent = value;
    }
    public IRequestSpan ChildSpan(string name)
    {
        return innerSpan.ChildSpan(name);
    }

    public bool CanWrite => innerSpan.CanWrite;
    public string Id => innerSpan.Id;
    public uint? Duration => innerSpan.Duration;
}
