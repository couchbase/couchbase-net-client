using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;

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
            return _nodes.Remove(nodeToRemove);
        }
    }

    public IList<IClusterNode> Clear(IBucket bucket)
    {
        lock (_nodes)
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
    /// Gets a node from the collection by <see cref="IPEndPoint"/>, if present.
    /// </summary>
    /// <param name="endPoint"><see cref="IPEndPoint"/> to find.</param>
    /// <param name="bucketName">The bucketName that may be the owner.</param>
    /// <param name="node">Node found, if any.</param>
    /// <returns>True if the node was found.</returns>
    /// <remarks>
    /// Optimized to be low-lock.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGet(HostEndpointWithPort endPoint, string bucketName, [NotNullWhen(true)] out IClusterNode? node)
    {
        // ReSharper disable once InconsistentlySynchronizedField
       node = _nodes.FirstOrDefault(x => x.EndPoint == endPoint && x.BucketName == bucketName);
       return node != null;
    }

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
