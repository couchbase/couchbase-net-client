#nullable enable
using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.Configuration.Server;
using Couchbase.Test.Common.Configuration;

namespace Couchbase.LoadTests.Core.Configuration
{
    /// <summary>
    /// CouchbaseBucket.ConfigUpdatedAsync with N nodes, for a config that the bucket applies. A vbucket map change rebuilds
    /// the key mapper. A node change also runs ProcessClusterMapAsync, which updates every node of the bucket.
    /// </summary>
    [MemoryDiagnoser]
    [InvocationCount(InvocationsPerIteration)]
    [Config(typeof(InProcessConfig))]
    public class BucketConfigUpdateBenchmarks
    {
        private const int InvocationsPerIteration = 512;

        private ConfigHarness _harness = null!;
        private CouchbaseBucket _bucket = null!;
        private ClusterMapBuilder _map = null!;
        private ClusterMapBuilder _mapWithQueryNode = null!;
        private BucketConfig[] _configs = [];
        private int _next;
        private ulong _rev = 1;

        [Params(3, 20, 100)]
        public int Nodes { get; set; }

        [GlobalSetup]
        public async Task Setup()
        {
            _harness = ConfigHarness.Create(BenchmarkMaps.Global(Nodes, rev: 1));
            _map = BenchmarkMaps.Bucket(Nodes, rev: _rev);
            _mapWithQueryNode = BenchmarkMaps.Bucket(Nodes, rev: _rev).Node(BenchmarkMaps.Host(Nodes - 1), n => n.Query());
            _bucket = await _harness.OpenBucketAsync(_map);
        }

        // A fixed invocation count lets each iteration parse its configs up front. The rev rises on every call,
        // so the bucket version gate lets each config through, and each config undoes the change of the one before.
        [IterationSetup(Target = nameof(VBucketMapChange))]
        public void PrepareVBucketMapChanges() => Prepare(changed =>
        {
            var config = BenchmarkMaps.Arrived(_map.Rev(_rev), _harness.Options);
            if (changed)
            {
                // Active and replica swap places on every vbucket.
                foreach (var vbucket in config.VBucketServerMap.VBucketMap)
                {
                    (vbucket[0], vbucket[1]) = (vbucket[1], vbucket[0]);
                }
            }
            return config;
        });

        // The last node starts and stops the query service, so the vbucket map stays the same.
        [IterationSetup(Target = nameof(NodeChange))]
        public void PrepareNodeChanges() => Prepare(changed =>
            BenchmarkMaps.Arrived((changed ? _mapWithQueryNode : _map).Rev(_rev), _harness.Options));

        [GlobalCleanup]
        public Task Cleanup() => _harness.DisposeAsync().AsTask();

        [Benchmark]
        public Task VBucketMapChange() => _bucket.ConfigUpdatedAsync(_configs[_next++]);

        [Benchmark]
        public Task NodeChange() => _bucket.ConfigUpdatedAsync(_configs[_next++]);

        private void Prepare(Func<bool, BucketConfig> build)
        {
            _configs = new BucketConfig[InvocationsPerIteration];
            for (var i = 0; i < _configs.Length; i++)
            {
                _rev++;
                // The bucket opened with the unchanged map at rev 1.
                _configs[i] = build(_rev % 2 == 0);
            }
            _next = 0;
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
