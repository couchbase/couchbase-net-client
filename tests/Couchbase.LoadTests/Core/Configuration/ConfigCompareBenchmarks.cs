#nullable enable
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.Configuration.Server;

namespace Couchbase.LoadTests.Core.Configuration
{
    /// <summary>
    /// The checks a subscriber runs on each config it gets. HasConfigChanges is the dedup gate, and a bucket runs the
    /// other two checks on every config that passes it.
    /// </summary>
    [MemoryDiagnoser]
    [Config(typeof(InProcessConfig))]
    public class ConfigCompareBenchmarks
    {
        public enum MapChange
        {
            Identical,
            RevOnly,
            NodeChange
        }

        private BucketConfig _current = null!;
        private BucketConfig _incoming = null!;

        [Params(3, 20, 100)]
        public int Nodes { get; set; }

        [Params(MapChange.Identical, MapChange.RevOnly, MapChange.NodeChange)]
        public MapChange Change { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _current = BenchmarkMaps.Bucket(Nodes, rev: 10).Build();
            var incoming = BenchmarkMaps.Bucket(Nodes, rev: Change == MapChange.Identical ? 10UL : 11UL);
            if (Change == MapChange.NodeChange)
            {
                // The last node starts the query service, so the vbucket map does not change.
                incoming.Node(BenchmarkMaps.Host(Nodes - 1), n => n.Query());
            }
            _incoming = incoming.Build();
        }

        [Benchmark]
        public bool ConfigEquals() => _incoming.Equals(_current);

        [Benchmark]
        public bool HasConfigChanges() => _incoming.HasConfigChanges(_current, BenchmarkMaps.BucketName);

        [Benchmark]
        public bool HasClusterNodesChanged() => _incoming.HasClusterNodesChanged(_current);

        [Benchmark]
        public bool HasVBucketMapChanged() => _incoming.HasVBucketMapChanged(_current, out _);
    }

    /// <summary>
    /// Reads <see cref="BucketConfig.ServerGroupNodeIndexes"/> on a map with 3 server groups, once per call like a zone aware
    /// replica read. The property is rebuilt on every read. CachedLookup does the same lookup on a dictionary built once.
    /// </summary>
    [MemoryDiagnoser]
    [Config(typeof(InProcessConfig))]
    public class ServerGroupNodeIndexesBenchmarks
    {
        private const string PreferredGroup = "group-1";

        private BucketConfig _config = null!;
        private Dictionary<string, int[]> _cached = null!;

        [Params(3, 20, 100)]
        public int Nodes { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _config = BenchmarkMaps.Bucket(Nodes, rev: 1, configure: (i, n) => n.ServerGroup($"group-{i % 3}")).Build();
            _config.NetworkResolution = NetworkResolution.Default;
            _cached = _config.ServerGroupNodeIndexes;
        }

        [Benchmark(Baseline = true)]
        public int[]? CachedLookup() => _cached.TryGetValue(PreferredGroup, out var indexes) ? indexes : null;

        [Benchmark]
        public int[]? ServerGroupNodeIndexes() =>
            _config.ServerGroupNodeIndexes.TryGetValue(PreferredGroup, out var indexes) ? indexes : null;
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
