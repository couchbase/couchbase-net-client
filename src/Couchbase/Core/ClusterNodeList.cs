using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using Couchbase.Core.Configuration.Server;

#nullable enable

namespace Couchbase.Core;

internal class ClusterNodeList : IEnumerable<IClusterNode>
{
    private readonly object _syncObj = new();

    /// <summary>
    /// Simple list of nodes, must be locked before using.
    /// </summary>
    private readonly HashSet<IClusterNode> _nodes = new();

    /// <summary>
    /// Number of nodes in the collection.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_syncObj)
            {
                return _nodes.Count;
            }
        }
    }

    /// <summary>
    /// Adds a node to the collection.
    /// </summary>
    /// <param name="node">Node to add.</param>
    /// <returns>True if added, false if already in the collection.</returns>
    public bool Add(IClusterNode node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        lock (_syncObj)
        {
            if (!_nodes.Add(node))
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Removes a node from the collection, if any.
    /// </summary>
    /// <param name="nodeToRemove">Node which was removed, if any.</param>
    /// <returns>True if the node was removed.</returns>
    public bool Remove(IClusterNode nodeToRemove)
    {
        lock (_syncObj)
        {
            // By reference, as nodes with no K/V service can share an endpoint and owner.
            var removeCount = _nodes.RemoveWhere(x => ReferenceEquals(x, nodeToRemove));
            return removeCount > 0;
        }
    }

    /// <summary>
    /// Removes all nodes owned by a bucket from the collection.
    /// </summary>
    /// <param name="bucket">Owner whose nodes should be removed.</param>
    /// <returns>List of nodes that were removed.</returns>
    public IList<IClusterNode> Clear(IBucket bucket)
    {
        lock (_syncObj)
        {
            var removed = new List<IClusterNode>(_nodes.Where(x=>x.Owner == bucket));

            _nodes.RemoveWhere(x => x.Owner == bucket);

            return removed;
        }
    }

    /// <summary>
    /// Removes all nodes from the collection.
    /// </summary>
    /// <returns>List of nodes that were removed.</returns>
    public IList<IClusterNode> Clear()
    {
        lock (_syncObj)
        {
            var removed = new List<IClusterNode>(_nodes);

            _nodes.Clear();

            return removed;
        }
    }

    /// <summary>
    /// Gets the node of a cluster map entry owned by a bucket, if present.
    /// </summary>
    /// <param name="endPoint">The K/V endpoint of the map entry.</param>
    /// <param name="nodeAdapter">The map entry.</param>
    /// <param name="bucketName">The bucketName that may be the owner.</param>
    /// <param name="node">Node found, if any.</param>
    /// <returns>True if the node was found.</returns>
    /// <remarks>
    /// Optimized to be low-lock.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet(HostEndpointWithPort endPoint, NodeAdapter nodeAdapter, string bucketName, [NotNullWhen(true)] out IClusterNode? node)
    {
        // ReSharper disable once InconsistentlySynchronizedField
       node = _nodes.FirstOrDefault(x => x.BucketName == bucketName && IsNodeOf(x, endPoint, nodeAdapter));
       return node != null;
    }

    /// <summary>
    /// Gets the node of a cluster map entry, whatever its owner, if present.
    /// </summary>
    /// <param name="endPoint">The K/V endpoint of the map entry.</param>
    /// <param name="nodeAdapter">The map entry.</param>
    /// <param name="node">Node found, if any.</param>
    /// <returns>True if the node was found.</returns>
    /// <remarks>
    /// Optimized to be low-lock.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet(HostEndpointWithPort endPoint, NodeAdapter nodeAdapter, [NotNullWhen(true)] out IClusterNode? node)
    {
        // ReSharper disable once InconsistentlySynchronizedField
        node = _nodes.FirstOrDefault(x => IsNodeOf(x, endPoint, nodeAdapter));
        return node != null;
    }

    /// <summary>
    /// Returns true when <paramref name="node"/> is the node of a cluster map entry.
    /// </summary>
    /// <param name="node">The node to check.</param>
    /// <param name="endPoint">The K/V endpoint of the map entry.</param>
    /// <param name="nodeAdapter">The map entry, if known.</param>
    /// <remarks>
    /// The endpoint must match, as a node keeps its endpoint and connections for life.
    /// Nodes on an alternate network can share a host, and nodes with no K/V service then share the endpoint.
    /// So the <see cref="NodeAdapter.NodeId"/> must also match when both sides have one.
    /// </remarks>
    public static bool IsNodeOf(IClusterNode node, HostEndpointWithPort endPoint, NodeAdapter? nodeAdapter) =>
        node.EndPoint.Equals(endPoint)
        && (node.NodesAdapter?.NodeId is not { } nodeId
            || nodeAdapter?.NodeId is not { } mapNodeId
            || nodeId.Equals(mapNodeId));

    /// <inheritdoc />
    /// <remarks>
    /// Thread-safe, takes a snapshot of the current state at the time it is executed.
    /// </remarks>
    public IEnumerator<IClusterNode> GetEnumerator()
    {
        lock (_syncObj)
        {
            return new List<IClusterNode>(_nodes).GetEnumerator();
        }
    }

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
