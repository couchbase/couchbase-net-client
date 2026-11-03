#nullable enable
using System.Collections.Generic;

namespace Couchbase.Test.Common.Configuration.Model
{
    /// <summary>
    /// One change to a <see cref="ClusterModel"/>. Each applied event is one commit, so the maps it changes get one new rev.
    /// </summary>
    internal abstract record ModelEvent
    {
        protected static string List<T>(IEnumerable<T> values) => $"[{string.Join(", ", values)}]";
    }

    /// <summary>
    /// New members join nodesExt, outside every bucket. Null services are the default services of the model.
    /// </summary>
    internal sealed record AddNodes(int Count, IReadOnlyList<string>? Services = null) : ModelEvent
    {
        public override string ToString() => Services is null ? $"AddNodes {Count}" : $"AddNodes {Count} {List(Services)}";
    }

    /// <summary>
    /// An ejected host joins again as a new member, with the same hostname and a new node UUID.
    /// </summary>
    internal sealed record RejoinNode(string Host, IReadOnlyList<string>? Services = null) : ModelEvent
    {
        public override string ToString() => Services is null ? $"RejoinNode {Host}" : $"RejoinNode {Host} {List(Services)}";
    }

    internal sealed record SetServices(string Host, IReadOnlyList<string> Services) : ModelEvent
    {
        public override string ToString() => $"SetServices {Host} {List(Services)}";
    }

    /// <summary>
    /// Data nodes join the bucket node list at the end. They hold no vbuckets until a rebalance.
    /// </summary>
    internal sealed record AddToBucket(string Bucket, IReadOnlyList<string> Hosts) : ModelEvent
    {
        public override string ToString() => $"AddToBucket {Bucket} {List(Hosts)}";
    }

    /// <summary>
    /// Sets the forward map. It spreads the vbuckets over the bucket nodes, except the leaving ones.
    /// </summary>
    internal sealed record StartRebalance(string Bucket, IReadOnlyList<string> Leaving) : ModelEvent
    {
        public override string ToString() => $"StartRebalance {Bucket} leaving {List(Leaving)}";
    }

    /// <summary>
    /// Each listed vbucket chain becomes its forward chain.
    /// </summary>
    internal sealed record MoveVBuckets(string Bucket, IReadOnlyList<int> VBuckets) : ModelEvent
    {
        public override string ToString() => $"MoveVBuckets {Bucket} {List(VBuckets)}";
    }

    /// <summary>
    /// Clears the forward map once every vbucket has moved.
    /// </summary>
    internal sealed record FinishRebalance(string Bucket) : ModelEvent
    {
        public override string ToString() => $"FinishRebalance {Bucket}";
    }

    /// <summary>
    /// Hosts that hold no copy leave the bucket node list. They stay in nodesExt, after the bucket nodes.
    /// </summary>
    internal sealed record RemoveFromBucket(string Bucket, IReadOnlyList<string> Hosts) : ModelEvent
    {
        public override string ToString() => $"RemoveFromBucket {Bucket} {List(Hosts)}";
    }

    /// <summary>
    /// Hosts that are in no bucket leave nodesExt.
    /// </summary>
    internal sealed record EjectNodes(IReadOnlyList<string> Hosts) : ModelEvent
    {
        public override string ToString() => $"EjectNodes {List(Hosts)}";
    }

    /// <summary>
    /// A new version of one map with no other change. A null bucket is the cluster map.
    /// </summary>
    internal sealed record BumpRev(string? Bucket) : ModelEvent
    {
        public override string ToString() => $"BumpRev {Bucket ?? "cluster"}";
    }
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
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
