#nullable enable
using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.Configuration.Server;
using Couchbase.Test.Common.Configuration;

namespace Couchbase.LoadTests.Core.Configuration
{
    /// <summary>
    /// ClusterContext.ConfigUpdatedAsync with N global nodes. Bug 1 keeps GlobalConfig at the bootstrap rev, so the latest
    /// map delivered again (LatestAgain) reprocesses every node like a new rev (NextRev, the baseline of the Ratio column).
    /// Polling delivers that map for each node every 2.5 s. SameAsGlobalConfig is the cost of a repeat once the bug is fixed.
    /// </summary>
    [MemoryDiagnoser]
    [InvocationCount(InvocationsPerIteration)]
    [Config(typeof(InProcessConfig))]
    public class GlobalConfigUpdateBenchmarks
    {
        private const int InvocationsPerIteration = 1024;

        private ConfigHarness _harness = null!;
        private ClusterMapBuilder _map = null!;
        private BucketConfig _bootstrapCopy = null!;
        private BucketConfig _latest = null!;
        private BucketConfig[] _nextRevs = [];
        private int _next;
        private ulong _rev = 2;

        [Params(3, 20, 100)]
        public int Nodes { get; set; }

        [GlobalSetup]
        public async Task Setup()
        {
            _map = BenchmarkMaps.Global(Nodes, rev: 1);
            // Real time keeps the fake clock bookkeeping out of the measured cost.
            _harness = ConfigHarness.Create(_map, clock: TimeProvider.System);
            _bootstrapCopy = BenchmarkMaps.Arrived(_map, _harness.Options);
            _latest = BenchmarkMaps.Arrived(_map.Rev(_rev), _harness.Options);
            await _harness.Context.ConfigUpdatedAsync(_latest);
        }

        // A fixed invocation count lets each iteration parse its configs up front, so NextRev gets a higher rev on every call.
        [IterationSetup(Target = nameof(NextRev))]
        public void PrepareNextRevs()
        {
            _nextRevs = new BucketConfig[InvocationsPerIteration];
            for (var i = 0; i < _nextRevs.Length; i++)
            {
                _nextRevs[i] = BenchmarkMaps.Arrived(_map.Rev(++_rev), _harness.Options);
            }
            _next = 0;
        }

        [GlobalCleanup]
        public Task Cleanup() => _harness.DisposeAsync().AsTask();

        [Benchmark]
        public Task SameAsGlobalConfig() => _harness.Context.ConfigUpdatedAsync(_bootstrapCopy);

        [Benchmark]
        public Task LatestAgain() => _harness.Context.ConfigUpdatedAsync(_latest);

        [Benchmark(Baseline = true)]
        public Task NextRev() => _harness.Context.ConfigUpdatedAsync(_nextRevs[_next++]);
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
