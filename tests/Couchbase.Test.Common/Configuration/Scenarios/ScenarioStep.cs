#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Test.Common.Configuration.Model;

namespace Couchbase.Test.Common.Configuration.Scenarios
{
    /// <summary>
    /// One step of a <see cref="Scenario"/>. Its text is the line of the step log.
    /// </summary>
    internal abstract record ScenarioStep;

    /// <summary>
    /// A change to the cluster, played as model events. Each event reads the model state, so the runner applies each
    /// event before it reads the next.
    /// </summary>
    internal sealed record ClusterStep(string Name, Func<ClusterModel, IEnumerable<ModelEvent>> Events, DuringSteps? During)
        : ScenarioStep
    {
        public override string ToString() => Name;
    }

    /// <summary>
    /// Steps that run at coarse points of the first rebalance of a <see cref="ClusterStep"/>.
    /// </summary>
    internal sealed record DuringSteps(IReadOnlyList<ScenarioStep> AfterStart, IReadOnlyList<ScenarioStep> Halfway,
        IReadOnlyList<ScenarioStep> BeforeFinish);

    /// <summary>
    /// A change to how one host behaves on the network of <see cref="ConnectionMode.Real"/>.
    /// </summary>
    internal sealed record NetworkStep(string Name, string Host, Action<FakeClusterServer, string> Apply) : ScenarioStep
    {
        public override string ToString() => $"{Name} {Host}";
    }

    /// <summary>
    /// The host gets no delivery until <see cref="CatchUpStep"/>.
    /// </summary>
    internal sealed record LagStep(string Host) : ScenarioStep
    {
        public override string ToString() => $"Lag {Host}";
    }

    /// <summary>
    /// The host gets the latest maps and takes deliveries again.
    /// </summary>
    internal sealed record CatchUpStep(string Host) : ScenarioStep
    {
        public override string ToString() => $"CatchUp {Host}";
    }

    /// <summary>
    /// Sets whether the next deliveries notify the connections.
    /// </summary>
    internal sealed record PushStep(bool Notify) : ScenarioStep
    {
        public override string ToString() => Notify ? "Push" : "NoPush";
    }

    internal sealed record WaitStep(TimeSpan Duration) : ScenarioStep
    {
        public override string ToString() => $"Wait {Duration.TotalSeconds} s";
    }

    internal sealed record DoStep(Func<ScenarioContext, Task> Action) : ScenarioStep
    {
        public override string ToString() => "Do";
    }

    /// <summary>
    /// Steps with no check after them.
    /// </summary>
    internal sealed record UncheckedStep(IReadOnlyList<ScenarioStep> Steps) : ScenarioStep
    {
        public override string ToString() => "Unchecked";
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
