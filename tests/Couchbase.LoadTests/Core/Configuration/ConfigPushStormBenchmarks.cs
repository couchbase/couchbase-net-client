#nullable enable
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using Couchbase.Test.Common.Configuration;

namespace Couchbase.LoadTests.Core.Configuration
{
    /// <summary>
    /// Brief pushes for one new version from all nodes, measured until the bucket has applied it. Every node pushes each
    /// new version, so a storm must cost one fetch. The Fetches/op column counts the fetches the fake server answered.
    /// </summary>
    [MemoryDiagnoser]
    [Config(typeof(FetchesConfig))]
    [InvocationCount(InvocationsPerIteration)]
    public class ConfigPushStormBenchmarks
    {
        private const int InvocationsPerIteration = 512;
        private const int NodeCount = 3;

        // Only guards against a hang.
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
        private static readonly ConcurrentDictionary<int, double> FetchesPerOperation = new();

        private ConfigHarness _harness = null!;
        private ClusterMapBuilder _map = null!;
        private string[] _hosts = [];
        private FakeClusterServer.ServedMap[] _maps = [];
        private int _next;
        private ulong _rev = 1;
        private long _operations;
        private long _fetches;

        [Params(1, 10, 100)]
        public int Pushes { get; set; }

        [GlobalSetup]
        public async Task Setup()
        {
            // Real time keeps the fake clock bookkeeping out of the measured cost.
            _harness = ConfigHarness.Create(BenchmarkMaps.Global(NodeCount, rev: 1), clock: TimeProvider.System);
            _map = BenchmarkMaps.Bucket(NodeCount, rev: _rev);
            await _harness.OpenBucketAsync(_map);
            _hosts = new string[NodeCount];
            for (var i = 0; i < NodeCount; i++)
            {
                _hosts[i] = BenchmarkMaps.Host(i);
            }
        }

        // A fixed invocation count lets each iteration serialize its maps up front, so every call pushes a higher rev.
        [IterationSetup]
        public void PrepareMaps()
        {
            _fetches += _harness.Server.Fetches.Count;
            _harness.Server.ClearFetches();

            _maps = new FakeClusterServer.ServedMap[InvocationsPerIteration];
            for (var i = 0; i < _maps.Length; i++)
            {
                _rev++;
                // Bug 6 keeps the bucket on its config for a rev only change, so each version also flips the replica count.
                _maps[i] = FakeClusterServer.Prepare(_map.Rev(_rev).VBuckets(1024, replicas: _rev % 2 == 0 ? 2 : 1));
            }
            _next = 0;
        }

        [GlobalCleanup]
        public async Task Cleanup()
        {
            _fetches += _harness.Server.Fetches.Count;
            FetchesPerOperation[Pushes] = (double)_fetches / _operations;
            await _harness.DisposeAsync();
        }

        [Benchmark]
        public async Task PushStorm()
        {
            _harness.ForgetDispatched();
            var map = _maps[_next++];
            _harness.Server.SetMap(map);
            for (var i = 0; i < Pushes; i++)
            {
                _harness.Server.PushBrief(_hosts[i % NodeCount], map.Version.Epoch, map.Version.Revision);
            }

            await _harness.WaitForPublishedAsync(BenchmarkMaps.BucketName, map.Version, Timeout);
            await _harness.DrainAsync();
            _operations++;
        }

        private sealed class FetchesConfig : InProcessConfig
        {
            public FetchesConfig()
            {
                AddColumn(new FetchesColumn());
            }
        }

        private sealed class FetchesColumn : IColumn
        {
            public string Id => nameof(FetchesColumn);
            public string ColumnName => "Fetches/op";
            public bool AlwaysShow => true;
            public ColumnCategory Category => ColumnCategory.Custom;
            public int PriorityInCategory => 0;
            public bool IsNumeric => true;
            public UnitType UnitType => UnitType.Dimensionless;
            public string Legend => "Config fetches the fake server answered per operation";

            public string GetValue(Summary summary, BenchmarkCase benchmarkCase) =>
                GetValue(summary, benchmarkCase, SummaryStyle.Default);

            public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style) =>
                FetchesPerOperation.TryGetValue((int)benchmarkCase.Parameters[nameof(Pushes)], out var fetches)
                    ? fetches.ToString("0.00", CultureInfo.InvariantCulture)
                    : "N/A";

            public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

            public bool IsAvailable(Summary summary) => true;
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
