#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Test.Common.Configuration.Model;
using Xunit;

namespace Couchbase.Test.Common.Configuration.Scenarios
{
    /// <summary>
    /// A cluster situation written as steps. The steps change a <see cref="ClusterModel"/>, the nodes of the fake server
    /// serve its maps, and a check compares the SDK buckets with the model after each change.
    /// </summary>
    /// <remarks>
    /// <code>
    /// await using var scenario = await Scenario.On(ClusterProfile.CapellaPublic, nodes: 4)
    ///     .Buckets("travel", "audit")
    ///     .RealConnections()
    ///     .StartAsync();
    ///
    /// await scenario
    ///     .ScaleIn("node-3")
    ///     .ScaleOut(2, services: ["kv", "n1ql"], during: at => at
    ///         .Halfway(s => s.Close("node-1")))
    ///     .FailOver("node-2", Failover.Graceful)
    ///     .Recover("node-2", Recovery.Delta)
    ///     .RunAsync();
    /// </code>
    /// Read <see cref="ScenarioRunner"/> for when the checks run, and <see cref="ScenarioCheck"/> for what they compare.
    /// </remarks>
    internal sealed class Scenario : ScenarioBuilder<Scenario>, IAsyncDisposable
    {
        private readonly ScenarioRunner _runner;

        internal Scenario(ScenarioContext context, ScenarioRunner runner) : base(context.Harness.Mode)
        {
            Context = context;
            _runner = runner;
        }

        /// <summary>
        /// Starts the setup of a scenario on the profile with that name.
        /// </summary>
        public static ScenarioSetup On(string profile, int nodes = 3) => On(ClusterProfile.Load(profile), nodes);

        public static ScenarioSetup On(ClusterProfile profile, int nodes = 3) => new(profile, nodes);

        public ScenarioContext Context { get; }

        public ConfigHarness Harness => Context.Harness;

        public ClusterModel Model => Context.Model;

        public ModelServer Server => Context.Server;

        /// <inheritdoc cref="ScenarioRunner.Log"/>
        public IReadOnlyList<string> Log => _runner.Log;

        /// <inheritdoc cref="ScenarioContext.Bucket"/>
        public CouchbaseBucket Bucket(string? name = null) => Context.Bucket(name);

        /// <inheritdoc cref="ScenarioContext.View"/>
        public ConfigHarness.BucketView View(string? name = null) => Context.View(name);

        /// <summary>
        /// Runs the steps added since the last run. It fails at the end when a check found a difference, and lists
        /// every difference and the step log.
        /// </summary>
        public async Task RunAsync()
        {
            var differences = await _runner.RunAsync(TakeSteps()).ConfigureAwait(false);
            if (differences.Count == 0) return;

            Assert.Fail($"The SDK differs from the model {differences.Count} times.\n{string.Join("\n", differences)}\n\n" +
                        $"Step log\n{string.Join("\n", Log)}");
        }

        public ValueTask DisposeAsync() => Harness.DisposeAsync();
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
