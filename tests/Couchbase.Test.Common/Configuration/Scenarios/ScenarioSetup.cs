#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Test.Common.Configuration.Model;

namespace Couchbase.Test.Common.Configuration.Scenarios
{
    /// <summary>
    /// Sets up a <see cref="Scenario"/>. By default it has the bucket of the profile, fake connections, the virtual
    /// clock, the SDK bootstrap and the SDK options of <see cref="ConfigHarness.Create"/> with config polling on.
    /// </summary>
    /// <remarks>
    /// When the profile nodes share one external host, like on Private Link, the SDK connects the way the captured SDK
    /// did. The connection string is couchbases:// with that host, so TLS is on and the seed is that host on 11207, which
    /// is in no map. The network resolution is auto, the SDK default, so the SDK picks the external network at bootstrap.
    /// Else the SDK connects to the first model node on the default network, with no TLS. <see cref="Configure"/> runs
    /// after this and can change it.
    /// </remarks>
    internal sealed class ScenarioSetup
    {
        /// <summary>
        /// The default of <see cref="Deadline"/>.
        /// </summary>
        public static readonly TimeSpan DefaultDeadline = TimeSpan.FromSeconds(10);

        // The SDK default, so that a test can change it with Configure.
        private static readonly TimeSpan PollInterval = new ClusterOptions().ConfigPollInterval;

        private readonly ClusterProfile _profile;
        private readonly int _nodes;
        private IReadOnlyList<string>? _buckets;
        private ConnectionMode _mode = ConnectionMode.Fake;
        private BootstrapMode _bootstrap = BootstrapMode.Sdk;
        private NetworkOptions? _network;
        private Action<ClusterOptions>? _configure;
        private TimeProvider? _clock;
        private TimeSpan _deadline = DefaultDeadline;
        private int _seed;

        internal ScenarioSetup(ClusterProfile profile, int nodes)
        {
            _profile = profile;
            _nodes = nodes;
        }

        /// <summary>
        /// The buckets of the model. Every node is in every bucket, and the scenario opens each one.
        /// </summary>
        public ScenarioSetup Buckets(params string[] names)
        {
            _buckets = names.ToList();
            return this;
        }

        public ScenarioSetup Connections(ConnectionMode mode)
        {
            _mode = mode;
            if (mode != ConnectionMode.Real) _network = null;
            return this;
        }

        /// <param name="network">The defaults of <see cref="NetworkOptions"/> when null.</param>
        public ScenarioSetup RealConnections(NetworkOptions? network = null)
        {
            _mode = ConnectionMode.Real;
            _network = network;
            return this;
        }

        /// <summary>
        /// Who creates the global nodes and opens the buckets. It is <see cref="BootstrapMode.Sdk"/> by default.
        /// </summary>
        public ScenarioSetup Bootstrap(BootstrapMode bootstrap)
        {
            _bootstrap = bootstrap;
            return this;
        }

        /// <summary>
        /// Changes the SDK options. Each call adds to the ones before.
        /// </summary>
        public ScenarioSetup Configure(Action<ClusterOptions> configure)
        {
            _configure += configure;
            return this;
        }

        /// <summary>
        /// Runs the SDK services on <see cref="TimeProvider.System"/>, for steps that need a timeout on real time.
        /// Each <see cref="ScenarioBuilder{TSelf}.Wait"/> then waits on real time.
        /// </summary>
        public ScenarioSetup RealTime()
        {
            _clock = TimeProvider.System;
            return this;
        }

        /// <summary>
        /// How long the runner gives the SDK to reach the latest maps after a step. It is real time, except with no push
        /// on the virtual clock. There the runner moves the clock one poll interval at a time, for up to the deadline.
        /// A bucket that misses it gets no new wait until the SDK catches up.
        /// </summary>
        public ScenarioSetup Deadline(TimeSpan deadline)
        {
            if (deadline <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(deadline), deadline, "The deadline must be positive.");
            _deadline = deadline;
            return this;
        }

        /// <summary>
        /// Seeds the vbucket planner and the order of the moves. It is 0 by default.
        /// </summary>
        public ScenarioSetup Seed(int seed)
        {
            _seed = seed;
            return this;
        }

        /// <summary>
        /// Builds the model, bootstraps the SDK on its cluster map, opens every bucket and makes each node serve its maps.
        /// </summary>
        public async Task<Scenario> StartAsync()
        {
            var model = ClusterModel.Create(_profile, _nodes, _buckets, _seed);
            var harness = ConfigHarness.Create(model.Render(model.Cluster, null), CapturedConnection() + _configure,
                PollInterval, _clock, _mode, _network, _bootstrap);
            try
            {
                harness.StartPolling();
                var buckets = new Dictionary<string, CouchbaseBucket>(StringComparer.Ordinal);
                foreach (var name in model.Buckets)
                {
                    buckets[name] = await harness.OpenBucketAsync(model.Render(model.Bucket(name), null)).ConfigureAwait(false);
                }

                var server = new ModelServer(model, harness.Server);
                server.Seed();
                var context = new ScenarioContext(harness, model, server, buckets);
                return new Scenario(context, new ScenarioRunner(context, new ScenarioCheck(context, _deadline)));
            }
            catch
            {
                await harness.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        private Action<ClusterOptions>? CapturedConnection()
        {
            if (_profile.NodeShape.AlternateAddresses.FirstOrDefault(a => a.SharedHostname is not null)?.SharedHostname is not { } host)
            {
                return null;
            }

            return options =>
            {
                options.ConnectionString = $"couchbases://{host}";
                options.NetworkResolution = NetworkResolution.Auto;
            };
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
