#nullable enable
using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.Configuration.Server;
using Couchbase.Test.Common.Configuration;

namespace Couchbase.LoadTests.Core.Configuration
{
    /// <summary>
    /// The cost per config of the config handler queue, which hands each config to every subscriber and waits for all of
    /// them. The maps go to the open buckets in turn and only raise the rev, so each bucket runs its compare and keeps its map.
    /// </summary>
    [MemoryDiagnoser]
    [InvocationCount(InvocationsPerIteration)]
    [Config(typeof(InProcessConfig))]
    public class ConfigHandlerThroughputBenchmarks
    {
        private const int ConfigsPerInvocation = 100;
        private const int InvocationsPerIteration = 16;
        private const int NodeCount = 3;

        private ConfigHarness _harness = null!;
        private ClusterMapBuilder[] _maps = [];
        private BucketConfig[] _configs = [];
        private int _next;
        private ulong _rev = 1;

        /// <summary>
        /// The maps published before each drain. Every call publishes 100 maps.
        /// </summary>
        [Params(10, 100)]
        public int Maps { get; set; }

        [Params(1, 5)]
        public int Buckets { get; set; }

        [GlobalSetup]
        public async Task Setup()
        {
            // Real time keeps the fake clock bookkeeping out of the measured cost.
            _harness = ConfigHarness.Create(BenchmarkMaps.Global(NodeCount, rev: 1), clock: TimeProvider.System);
            _maps = new ClusterMapBuilder[Buckets];
            for (var i = 0; i < Buckets; i++)
            {
                _maps[i] = BenchmarkMaps.Bucket(NodeCount, rev: _rev, name: $"{BenchmarkMaps.BucketName}-{i}");
                await _harness.OpenBucketAsync(_maps[i]);
            }
        }

        // A fixed invocation count lets each iteration parse its configs up front, so every bucket gets a higher rev each time.
        [IterationSetup]
        public void PrepareConfigs()
        {
            _configs = new BucketConfig[InvocationsPerIteration * ConfigsPerInvocation];
            for (var i = 0; i < _configs.Length; i++)
            {
                _configs[i] = BenchmarkMaps.Arrived(_maps[i % Buckets].Rev(++_rev), _harness.Options);
            }
            _next = 0;
        }

        [GlobalCleanup]
        public Task Cleanup() => _harness.DisposeAsync().AsTask();

        [Benchmark(OperationsPerInvoke = ConfigsPerInvocation)]
        public async Task PublishAndDrain()
        {
            _harness.ForgetDispatched();
            for (var i = 1; i <= ConfigsPerInvocation; i++)
            {
                _harness.Publish(_configs[_next++]);
                if (i % Maps == 0)
                {
                    await _harness.DrainAsync();
                }
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
