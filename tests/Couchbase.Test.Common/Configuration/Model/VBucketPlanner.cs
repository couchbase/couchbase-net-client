#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Test.Common.Configuration.Model
{
    /// <summary>
    /// Places the vbucket copies of a bucket, for its first map and for the forward map of a rebalance.
    /// </summary>
    /// <remarks>
    /// A node is an index into the bucket node list, and -1 is a missing copy. Each copy level is balanced over the
    /// target nodes, within 1. A copy stays on its node when the rules allow it, so a rebalance moves few copies.
    /// The planner walks the vbuckets in a new seeded random order for each copy level, so kept and moved copies
    /// interleave, and a vbucket that moves its active copy does not always move its replicas too.
    /// When the target nodes are in more than one server group, a replica is in another group than its active copy.
    /// </remarks>
    internal static class VBucketPlanner
    {
        // Scores compare the quota left first and a second criterion after it.
        private const long Weight = 1_000_000;

        /// <param name="current">The chains now, all -1 for the first map of a bucket.</param>
        /// <param name="target">The nodes that hold the copies at the end.</param>
        /// <param name="groups">The server group of each bucket node.</param>
        /// <returns>The chains at the end, with the same indexes. A chain has at most one copy per target node.</returns>
        public static short[][] Plan(IReadOnlyList<short[]> current, IReadOnlyList<int> target,
            IReadOnlyList<string?> groups, int replicas, Random random)
        {
            var nodes = groups.Count;
            var copies = replicas + 1;
            Check(current, target, nodes, copies);

            var plan = new short[current.Count][];
            for (var vbucket = 0; vbucket < plan.Length; vbucket++)
            {
                plan[vbucket] = Enumerable.Repeat((short)-1, copies).ToArray();
            }

            var walk = new Walk(current, target, groups, plan, random);
            walk.PlaceActives();
            for (var level = 1; level < copies && level < target.Count; level++)
            {
                walk.PlaceReplicas(level);
            }
            return plan;
        }

        private static void Check(IReadOnlyList<short[]> current, IReadOnlyList<int> target, int nodes, int copies)
        {
            if (target.Count == 0) throw new ArgumentException("The plan needs at least one target node.", nameof(target));
            if (target.Distinct().Count() != target.Count || target.Any(t => t < 0 || t >= nodes))
            {
                throw new ArgumentException($"The target nodes must be distinct indexes below {nodes}.", nameof(target));
            }

            for (var vbucket = 0; vbucket < current.Count; vbucket++)
            {
                var chain = current[vbucket];
                if (chain.Length != copies || chain.Any(c => c < -1 || c >= nodes))
                {
                    throw new ArgumentException(
                        $"Chain {vbucket} must have {copies} copies that are -1 or indexes below {nodes}.", nameof(current));
                }
            }
        }

        private static int[] Shuffle(int count, Random random)
        {
            var order = Enumerable.Range(0, count).ToArray();
            for (var i = count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            return order;
        }

        private sealed class Walk(IReadOnlyList<short[]> current, IReadOnlyList<int> target, IReadOnlyList<string?> groups,
            short[][] plan, Random random)
        {
            private readonly bool[] _inTarget = InTarget(target, groups.Count);
            private readonly bool _manyGroups = target.Select(t => groups[t]).Distinct().Count() > 1;
            private readonly int[,] _pairs = new int[groups.Count, groups.Count];

            public void PlaceActives()
            {
                var order = Shuffle(current.Count, random);
                var quota = Quotas(0);
                var count = new int[groups.Count];

                // The first pass only keeps copies. New copies placed in the same pass would fill nodes that still have
                // copies to keep, so more copies would move.
                foreach (var vbucket in order)
                {
                    var active = current[vbucket][0];
                    if (active < 0 || !_inTarget[active] || count[active] >= quota[active]) continue;
                    plan[vbucket][0] = active;
                    count[active]++;
                }

                foreach (var vbucket in order)
                {
                    if (plan[vbucket][0] >= 0) continue;
                    var chain = current[vbucket];
                    var node = Pick(n => count[n] < quota[n],
                        n => (Array.IndexOf(chain, (short)n, 1) > 0 ? Weight * Weight : 0) + quota[n] - count[n]);
                    plan[vbucket][0] = (short)node;
                    count[node]++;
                }
            }

            public void PlaceReplicas(int level)
            {
                var order = Shuffle(current.Count, random);
                var quota = Quotas(level);
                var count = new int[groups.Count];

                foreach (var vbucket in order)
                {
                    var replica = current[vbucket][level];
                    if (replica < 0 || !Free(vbucket, level, replica) || !GroupFits(vbucket, level, replica) ||
                        count[replica] >= quota[replica]) continue;
                    Set(vbucket, level, replica, count);
                }

                foreach (var vbucket in order)
                {
                    if (plan[vbucket][level] >= 0) continue;
                    var active = plan[vbucket][0];
                    var strict = NeedsOtherGroup(vbucket, level);
                    Func<int, bool> fits = n => Free(vbucket, level, n) && (!strict || OtherGroup(vbucket, n));
                    Func<int, long> score = n => (quota[n] - count[n]) * Weight - _pairs[active, n];
                    var node = Pick(n => fits(n) && count[n] < quota[n], score);
                    // Only quotas give way, never the group rule.
                    if (node < 0) node = Pick(fits, score);
                    Set(vbucket, level, node, count);
                }

                Rebalance(level, order, quota, count);
            }

            /// <summary>
            /// Moves replicas off nodes over their quota, along a path of moves that ends on a node under its quota.
            /// The greedy pass can get stuck when the server groups leave few choices, and a path always exists when a
            /// balanced level exists.
            /// </summary>
            private void Rebalance(int level, int[] order, int[] quota, int[] count)
            {
                while (target.Any(n => count[n] > quota[n]))
                {
                    var path = FindPath(level, order, quota, count);
                    if (path is null) return;
                    foreach (var (vbucket, to) in path)
                    {
                        var from = plan[vbucket][level];
                        _pairs[plan[vbucket][0], from]--;
                        count[from]--;
                        Set(vbucket, level, to, count);
                    }
                }
            }

            private List<(int VBucket, int To)>? FindPath(int level, int[] order, int[] quota, int[] count)
            {
                var via = new (int From, int VBucket)?[groups.Count];
                var seen = new bool[groups.Count];
                var queue = new Queue<int>();
                foreach (var node in target.Where(n => count[n] > quota[n]))
                {
                    seen[node] = true;
                    queue.Enqueue(node);
                }

                while (queue.Count > 0)
                {
                    var from = queue.Dequeue();
                    foreach (var vbucket in order)
                    {
                        if (plan[vbucket][level] != from) continue;
                        var strict = NeedsOtherGroup(vbucket, level);
                        foreach (var to in target)
                        {
                            if (seen[to] || !Free(vbucket, level, to) || (strict && !OtherGroup(vbucket, to))) continue;
                            seen[to] = true;
                            via[to] = (from, vbucket);
                            if (count[to] < quota[to]) return Unwind(via, to);
                            queue.Enqueue(to);
                        }
                    }
                }
                return null;
            }

            private static List<(int VBucket, int To)> Unwind((int From, int VBucket)?[] via, int end)
            {
                var path = new List<(int VBucket, int To)>();
                for (var node = end; via[node] is { } step; node = step.From)
                {
                    path.Add((step.VBucket, node));
                }
                return path;
            }

            private void Set(int vbucket, int level, int node, int[] count)
            {
                plan[vbucket][level] = (short)node;
                count[node]++;
                _pairs[plan[vbucket][0], node]++;
            }

            /// <summary>
            /// The share of each target node, within 1. The extra shares go to the nodes that hold the most copies of
            /// the level now, so fewer copies move. Ties go by the seed.
            /// </summary>
            private int[] Quotas(int level)
            {
                var held = new int[groups.Count];
                foreach (var chain in current)
                {
                    if (chain[level] >= 0) held[chain[level]]++;
                }

                var quota = new int[groups.Count];
                var share = current.Count / target.Count;
                var extra = current.Count % target.Count;
                var ranked = target.Select(n => (Node: n, Tie: random.Next())).OrderByDescending(n => held[n.Node])
                    .ThenBy(n => n.Tie).Select(n => n.Node).ToList();
                for (var i = 0; i < ranked.Count; i++)
                {
                    quota[ranked[i]] = share + (i < extra ? 1 : 0);
                }
                return quota;
            }

            /// <summary>
            /// The fitting node with the highest score. Ties go by the seed.
            /// </summary>
            private int Pick(Func<int, bool> fits, Func<int, long> score)
            {
                var best = -1;
                var bestScore = 0L;
                var ties = 0;
                foreach (var node in target)
                {
                    if (!fits(node)) continue;
                    var value = score(node);
                    if (best < 0 || value > bestScore)
                    {
                        best = node;
                        bestScore = value;
                        ties = 1;
                    }
                    else if (value == bestScore && random.Next(++ties) == 0)
                    {
                        best = node;
                    }
                }
                return best;
            }

            // True when the node is a target node and holds no other copy of the planned chain.
            private bool Free(int vbucket, int level, int node)
            {
                if (!_inTarget[node]) return false;
                var chain = plan[vbucket];
                for (var copy = 0; copy < chain.Length; copy++)
                {
                    if (copy != level && chain[copy] == node) return false;
                }
                return true;
            }

            private bool OtherGroup(int vbucket, int node) => !_manyGroups || groups[node] != groups[plan[vbucket][0]];

            private bool GroupFits(int vbucket, int level, int node) => OtherGroup(vbucket, node) || !NeedsOtherGroup(vbucket, level);

            // The group rule applies when a free node in another group exists.
            private bool NeedsOtherGroup(int vbucket, int level) =>
                _manyGroups && target.Any(n => Free(vbucket, level, n) && OtherGroup(vbucket, n));

            private static bool[] InTarget(IReadOnlyList<int> target, int nodes)
            {
                var inTarget = new bool[nodes];
                foreach (var node in target) inTarget[node] = true;
                return inTarget;
            }
        }
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
