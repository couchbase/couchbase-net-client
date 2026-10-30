#nullable enable
using System;
using BenchmarkDotNet.Attributes;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Test.Common.Configuration;

namespace Couchbase.LoadTests.Core.Configuration
{
    /// <summary>
    /// Turns cluster map bytes into a <see cref="BucketConfig"/> like a CCCP fetch does. ParseOnArrival adds the work the
    /// fetch does next. Every fetch, full push and poll pays this, and polling fetches from each node every 2.5 s.
    /// </summary>
    [MemoryDiagnoser]
    [Config(typeof(InProcessConfig))]
    public class ConfigParseBenchmarks
    {
        public enum MapKind
        {
            Bucket,
            Global
        }

        // The transcoder ClusterContext gives to GetClusterConfig operations.
        private readonly ITypeTranscoder _transcoder =
            new JsonTranscoder(SystemTextJsonSerializer.Create(InternalSerializationContext.Default));

        private ReadOnlyMemory<byte> _json;
        private string _bootstrapHost = string.Empty;

        [Params(3, 20, 100)]
        public int Nodes { get; set; }

        [Params(MapKind.Bucket, MapKind.Global)]
        public MapKind Map { get; set; }

        /// <summary>
        /// On the external network every node has an alternate address, and the vbucket server list is rewritten to use it.
        /// </summary>
        [Params(NetworkResolution.Default, NetworkResolution.External)]
        public string Network { get; set; } = NetworkResolution.Default;

        [GlobalSetup]
        public void Setup()
        {
            Action<int, NodeBuilder>? external = Network == NetworkResolution.External ? BenchmarkMaps.External : null;
            var map = Map == MapKind.Bucket
                ? BenchmarkMaps.Bucket(Nodes, rev: 1, configure: external)
                : BenchmarkMaps.Global(Nodes, rev: 1, external);
            _json = map.ToUtf8Bytes();
            _bootstrapHost = BenchmarkMaps.Host(0);
        }

        // The return type is object because BucketConfig is internal.
        [Benchmark(Baseline = true)]
        public object Parse() => Decode();

        // The steps of ClusterNode.GetClusterMap after the parse.
        [Benchmark]
        public object ParseOnArrival()
        {
            var config = Decode();
            config.ReplacePlaceholderWithBootstrapHost(_bootstrapHost);
            config.NetworkResolution = Network;
            return config;
        }

        private BucketConfig Decode() => _transcoder.Decode<BucketConfig>(_json, default, OpCode.GetClusterConfig)!;
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
