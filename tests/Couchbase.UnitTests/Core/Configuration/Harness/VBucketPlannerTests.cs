#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Couchbase.Test.Common.Configuration;
using Couchbase.Test.Common.Configuration.Model;
using Xunit;
using Xunit.Abstractions;
using static Couchbase.UnitTests.Core.Configuration.Harness.ClusterModelTests;
using static Couchbase.UnitTests.Core.Configuration.Harness.ClusterProfileTests;

namespace Couchbase.UnitTests.Core.Configuration.Harness
{
    public class VBucketPlannerTests(ITestOutputHelper output)
    {
        /// <summary>
        /// The longest run of one active node in the real 3 node map of Private Link.
        /// </summary>
        private const int RealLongestActiveRun = 13;

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void InitialMap_ThreeNodes_IsBalancedAndInterleaved(int seed)
        {
            var empty = Enumerable.Range(0, 1024).Select(_ => new short[] { -1, -1 }).ToArray();

            var plan = VBucketPlanner.Plan(empty, [0, 1, 2], ["group:2", "group:3", "group:1"], 1, new Random(seed));

            AssertWithinOne(CopiesPerServer(plan, 3, chain => chain.Take(1)));
            AssertWithinOne(CopiesPerServer(plan, 3, chain => chain.Skip(1)));
            var pairs = plan.GroupBy(c => (c[0], c[1])).Select(g => g.Count()).ToList();
            var longest = LongestActiveRun(plan);
            output.WriteLine($"pairs {string.Join(", ", pairs.OrderBy(p => p))}, longest active run {longest}");
            Assert.Equal(6, pairs.Count);
            // Even is 1024 / 6, about 171. The real map has pairs from 168 to 173.
            Assert.All(pairs, count => Assert.InRange(count, 166, 176));
            Assert.InRange(longest, 1, RealLongestActiveRun);
        }

        /// <summary>
        /// The two new nodes must get 409 active copies, so 409 is the fewest active moves. The real forward map moves 409
        /// active copies and changes 653 chains.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void ForwardMap_PrivateLinkThreeToFive_MovesFewestActives(int seed)
        {
            var example = Read(ClusterProfile.CapellaPrivateLink, "bucket-5nodes-forward-map.json");

            var plan = VBucketPlanner.Plan(example.Chains, [0, 1, 2, 3, 4], example.Groups, 1, new Random(seed));

            var activesMoved = Count(example.Chains, plan, (a, b) => a[0] != b[0]);
            var chainsChanged = Count(example.Chains, plan, (a, b) => !a.SequenceEqual(b));
            output.WriteLine($"actives moved {activesMoved}, chains changed {chainsChanged}");
            Assert.Equal(409, activesMoved);
            Assert.InRange(chainsChanged, 409, 700);
            AssertBalanced(plan, 5);
            Assert.All(plan, chain => Assert.NotEqual(example.Groups[chain[0]], example.Groups[chain[1]]));
        }

        /// <summary>
        /// The leaving node holds a fifth of the active copies, and the other nodes are under their new share, so only the
        /// copies of the leaving node move.
        /// </summary>
        [Theory]
        [InlineData(ClusterProfile.CapellaPrivateLink, "node-3.example.com")]
        [InlineData(ClusterProfile.CapellaPublic, "node-0.example.com")]
        public void ForwardMap_FiveToFour_MovesOnlyActivesOfLeavingNode(string profile, string leaving)
        {
            var example = Read(profile, "bucket-5nodes-stable.json");
            var gone = example.Hosts.IndexOf(leaving);
            var target = Enumerable.Range(0, 5).Where(i => i != gone).ToList();

            var plan = VBucketPlanner.Plan(example.Chains, target, example.Groups, 1, new Random(0));

            var activesMoved = Count(example.Chains, plan, (a, b) => a[0] != b[0]);
            var chainsChanged = Count(example.Chains, plan, (a, b) => !a.SequenceEqual(b));
            output.WriteLine($"actives moved {activesMoved}, chains changed {chainsChanged}");
            Assert.Equal(example.Chains.Count(c => c[0] == gone), activesMoved);
            Assert.All(plan, chain => Assert.DoesNotContain((short)gone, chain));
            Assert.All(plan, chain => Assert.NotEqual(example.Groups[chain[0]], example.Groups[chain[1]]));
            AssertWithinOne(target.Select(n => plan.Count(c => c[0] == n)).ToList());
            AssertWithinOne(target.Select(n => plan.Count(c => c[1] == n)).ToList());
        }

        [Fact]
        public void ForwardMap_DockerTwoToThree_BalancesInOneGroup()
        {
            var example = Read(ClusterProfile.Docker, "bucket-3nodes-forward-map.json");

            var plan = VBucketPlanner.Plan(example.Chains, [0, 1, 2], example.Groups, 1, new Random(0));

            Assert.Equal(341, Count(example.Chains, plan, (a, b) => a[0] != b[0]));
            AssertBalanced(plan, 3);
        }

        [Fact]
        public void Plan_FewerTargetNodesThanCopies_LeavesCopiesMissing()
        {
            var empty = Enumerable.Range(0, 64).Select(_ => new short[] { -1, -1, -1 }).ToArray();

            var one = VBucketPlanner.Plan(empty, [1], ["a", "a"], 2, new Random(0));
            var two = VBucketPlanner.Plan(empty, [0, 1], ["a", "b"], 2, new Random(0));

            Assert.All(one, chain => Assert.Equal(new short[] { 1, -1, -1 }, chain));
            Assert.All(two, chain => Assert.Equal(-1, chain[2]));
            AssertBalanced(two, 2);
        }

        [Fact]
        public void Plan_SameSeed_GivesSameMap()
        {
            var empty = Enumerable.Range(0, 1024).Select(_ => new short[] { -1, -1 }).ToArray();
            string[] groups = ["Group 1", "Group 1", "Group 1"];

            var first = VBucketPlanner.Plan(empty, [0, 1, 2], groups, 1, new Random(5));
            var second = VBucketPlanner.Plan(empty, [0, 1, 2], groups, 1, new Random(5));
            var other = VBucketPlanner.Plan(empty, [0, 1, 2], groups, 1, new Random(6));

            Assert.Equal(first, second);
            Assert.NotEqual(first, other);
        }

        private static void AssertBalanced(short[][] plan, int nodes)
        {
            AssertWithinOne(CopiesPerServer(plan, nodes, chain => chain.Take(1)));
            for (var level = 1; level < plan[0].Length && level < nodes; level++)
            {
                var copy = level;
                AssertWithinOne(CopiesPerServer(plan, nodes, chain => chain.Skip(copy).Take(1)));
            }
            Assert.All(plan, chain => Assert.Equal(chain.Count(c => c >= 0), chain.Where(c => c >= 0).Distinct().Count()));
        }

        private static int Count(short[][] before, short[][] after, Func<short[], short[], bool> changed) =>
            before.Where((chain, vbucket) => changed(chain, after[vbucket])).Count();

        private static int LongestActiveRun(short[][] plan)
        {
            var longest = 1;
            var run = 1;
            for (var vbucket = 1; vbucket < plan.Length; vbucket++)
            {
                run = plan[vbucket][0] == plan[vbucket - 1][0] ? run + 1 : 1;
                longest = Math.Max(longest, run);
            }
            return longest;
        }

        /// <summary>
        /// The server list hosts, their server groups and the current vbucket map of an example.
        /// </summary>
        private static (List<string> Hosts, List<string?> Groups, short[][] Chains) Read(string profile, string example)
        {
            using var doc = JsonDocument.Parse(ClusterProfile.Load(profile).Example(example).Json);
            var root = doc.RootElement;
            var groups = root.GetProperty("nodesExt").EnumerateArray()
                .ToDictionary(n => n.GetProperty("hostname").GetString()!, n => n.GetProperty("serverGroup").GetString());
            var map = root.GetProperty("vBucketServerMap");
            var hosts = map.GetProperty("serverList").EnumerateArray().Select(s => s.GetString()!.Split(':')[0]).ToList();
            var chains = map.GetProperty("vBucketMap").EnumerateArray()
                .Select(chain => chain.EnumerateArray().Select(c => c.GetInt16()).ToArray())
                .ToArray();
            return (hosts, hosts.Select(h => groups[h]).ToList(), chains);
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
