using System;

namespace Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
#nullable enable

public class NodeAndBucket : IEquatable<NodeAndBucket>
{
    public string Node { get; }

    public string? NodeUuid { get; }
    public string? AlternateNode { get; }
    public string? Bucket { get; }

    public NodeAndBucket(string node, string? alternateNode = null, string? nodeUuid = null, string? bucket = null)
    {
        Node = node;
        NodeUuid = nodeUuid;
        AlternateNode = alternateNode;
        Bucket = bucket;
    }

    public bool Equals(NodeAndBucket? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Node == other.Node && AlternateNode == other.AlternateNode && NodeUuid == other.NodeUuid && Bucket == other.Bucket;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((NodeAndBucket)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (Node.GetHashCode() * 397) ^
                   (AlternateNode != null ? AlternateNode.GetHashCode() : 0) ^
                   (Bucket != null ? Bucket.GetHashCode() : 0) ^
                   (NodeUuid != null ? NodeUuid.GetHashCode() : 0);
        }
    }

    public static bool operator == (NodeAndBucket left, NodeAndBucket right)
    {
        if (ReferenceEquals(left, null)) return ReferenceEquals(right, null);
        return left.Equals(right);
    }

    public static bool operator != (NodeAndBucket left, NodeAndBucket right)
    {
        return !(left == right);
    }
}
