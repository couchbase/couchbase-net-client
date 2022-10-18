using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;


#nullable enable

namespace Couchbase.Core
{
    /// <summary>
    /// Thread-safe collection of <see cref="IClusterNode"/> instances. Optimized to be low-lock
    /// for the common operation <see cref="TryGet"/>, but uses locks for Add and Remove operations.
    /// Also monitors <see cref="IClusterNode.KeyEndPointsChanged"/> to update the lookup dictionary.
    /// </summary>
    internal class ClusterNodeCollection : IEnumerable<IClusterNode>
    {
        /// <summary>
        /// Optimized dictionary of nodes for rapid, low-lock get operations.
        /// Allows nodes to be keyed by multiple keys.
        /// </summary>
        private readonly ConcurrentDictionary<HostEndpointWithPort, IClusterNode> _lookupDictionary = new();

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
                lock (_nodes)
                {
                    return _nodes.Count;
                }
            }
        }

        /// <summary>
        /// Gets a node from the collection by <see cref="IPEndPoint"/>, if present.
        /// </summary>
        /// <param name="endPoint"><see cref="IPEndPoint"/> to find.</param>
        /// <param name="node">Node found, if any.</param>
        /// <returns>True if the node was found.</returns>
        /// <remarks>
        /// Optimized to be low-lock.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(HostEndpointWithPort endPoint, [NotNullWhen(true)] out IClusterNode? node)
        {
            // ReSharper disable once InconsistentlySynchronizedField
            return _lookupDictionary.TryGetValue(endPoint, out node);
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

            lock (_nodes)
            {
                if (!_nodes.Add(node))
                {
                    return false;
                }

                node.KeyEndPointsChanged += OnKeyEndPointsChanged;

                foreach (var endPoint in node.KeyEndPoints)
                {
                    _lookupDictionary.TryAdd(endPoint, node);
                }

                return true;
            }
        }

        /// <summary>
        /// Removes a node from the collection, if any.
        /// </summary>
        /// <param name="endPoint"><see cref="IPEndPoint"/> of the node to remove.</param>
        /// <param name="removedNode">Node which was removed, if any.</param>
        /// <returns>True if the node was removed.</returns>
        public bool Remove(HostEndpointWithPort endPoint, [NotNullWhen(true)] out IClusterNode? removedNode)
        {
            lock (_nodes)
            {
                if (_lookupDictionary.TryRemove(endPoint, out removedNode))
                {
                    //remove all nodes for the endpoint as multiple buckets may exist
                    _nodes.RemoveWhere(x => x.EndPoint.Equals(endPoint));

                    removedNode.KeyEndPointsChanged -= OnKeyEndPointsChanged;

                    foreach (var additionalEndPoint in removedNode.KeyEndPoints)
                    {
                        // Also remove other keys referencing this endpoint
                        _lookupDictionary.TryRemove(additionalEndPoint, out _);
                    }

                    return true;
                }

                removedNode = null;
                return false;
            }
        }

        public IList<IClusterNode> Clear(IBucket bucket)
        {
            lock (_nodes)
            {
                var removed = new List<IClusterNode>(_nodes.Where(x=>x.Owner == bucket));

                _nodes.RemoveWhere(x => x.Owner == bucket);
                foreach (var clusterNode in removed.Where(clusterNode => _lookupDictionary.TryRemove(clusterNode.EndPoint, out _)))
                {
                    clusterNode.Dispose();
                }

                foreach (var removedNode in removed)
                {
                    removedNode.KeyEndPointsChanged -= OnKeyEndPointsChanged;
                }

                return removed;
            }
        }

        /// <summary>
        /// Removes all nodes from the collection.
        /// </summary>
        /// <returns>List of nodes that were removed.</returns>
        public IList<IClusterNode> Clear()
        {
            lock (_nodes)
            {
                var removed = new List<IClusterNode>(_nodes);

                _nodes.Clear();
                _lookupDictionary.Clear();

                foreach (var removedNode in removed)
                {
                    removedNode.KeyEndPointsChanged -= OnKeyEndPointsChanged;
                }

                return removed;
            }
        }

        private void OnKeyEndPointsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Move)
            {
                // noop
                return;
            }

            var node = (IClusterNode) sender!;

            lock (_nodes)
            {
                // Lock in case we're also in the midst of adding/removing a node right now,
                // even though _lookupDictionary is thread-safe.

                if (e.OldItems != null)
                {
                    foreach (var endPoint in e.OldItems.Cast<HostEndpointWithPort>())
                    {
                        _lookupDictionary.TryRemove(endPoint, out _);
                    }
                }

                if (e.NewItems != null)
                {
                    foreach (var endPoint in e.NewItems.Cast<HostEndpointWithPort>())
                    {
                        _lookupDictionary.TryAdd(endPoint, node);
                    }
                }
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Thread-safe, takes a snapshot of the current state at the time it is executed.
        /// </remarks>
        public IEnumerator<IClusterNode> GetEnumerator()
        {
            lock (_nodes)
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
