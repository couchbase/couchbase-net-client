#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Test.Common.Configuration.Model;

namespace Couchbase.Test.Common.Configuration.Scenarios
{
    /// <summary>
    /// Adds the steps of a <see cref="Scenario"/>. Each method adds one step and returns the builder.
    /// </summary>
    /// <remarks>
    /// A host is a model hostname such as node-1.example.com, or its first label such as node-1.
    /// The cluster steps play the event sequences of <see cref="ModelSequences"/> on every bucket. After each model
    /// event, every node that does not lag gets the latest maps.
    /// </remarks>
    internal abstract class ScenarioBuilder<TSelf> where TSelf : ScenarioBuilder<TSelf>
    {
        private readonly List<ScenarioStep> _steps = [];

        private protected ScenarioBuilder(ConnectionMode mode)
        {
            Mode = mode;
        }

        /// <summary>
        /// The connection mode of the scenario. Network steps need <see cref="ConnectionMode.Real"/>.
        /// </summary>
        public ConnectionMode Mode { get; }

        /// <summary>
        /// New kv nodes join and every bucket rebalances them in. Then they get their services.
        /// </summary>
        /// <param name="services">The default services of the profile when null.</param>
        /// <param name="during">Steps to run during the rebalance of the first bucket.</param>
        public TSelf ScaleOut(int count, IReadOnlyList<string>? services = null, Action<During>? during = null)
        {
            if (count < 1) throw new ArgumentOutOfRangeException(nameof(count), count, "A scale out adds at least one node.");
            var name = services is null ? $"ScaleOut {count}" : $"ScaleOut {count} {List(services)}";
            return Add(new ClusterStep(name, m => ModelSequences.ScaleOut(m, m.Buckets, count, services, m.Seed), Points(during)));
        }

        /// <summary>
        /// Every bucket rebalances the hosts out, then the hosts drop their services and leave the cluster.
        /// </summary>
        public TSelf ScaleIn(params string[] hosts) => AddScaleIn(hosts, null);

        /// <param name="during">Steps to run during the rebalance of the first bucket that holds the host.</param>
        public TSelf ScaleIn(string host, Action<During> during) => AddScaleIn([host], during);

        /// <param name="during">Steps to run during the rebalance of the first bucket that holds a host.</param>
        public TSelf ScaleIn(IReadOnlyList<string> hosts, Action<During> during) => AddScaleIn(hosts, during);

        /// <summary>
        /// New nodes join the cluster, outside every bucket, with no rebalance.
        /// </summary>
        /// <param name="services">The default services of the profile when null.</param>
        public TSelf AddNodes(int count, IReadOnlyList<string>? services = null)
        {
            var e = new AddNodes(count, services);
            return Add(new ClusterStep(e.ToString(), _ => [e], null));
        }

        /// <param name="during">Steps to run while the actives move off the host. Only a graceful failover takes them.</param>
        public TSelf FailOver(string host, Failover kind, Action<During>? during = null) => FailOver([host], kind, during);

        /// <summary>
        /// The hosts fail over and leave nodesExt. Read <see cref="Failover"/> for the kinds.
        /// </summary>
        /// <param name="during">Steps to run while the actives move off the hosts in the first bucket that holds one.
        /// Only a graceful failover takes them.</param>
        public TSelf FailOver(IReadOnlyList<string> hosts, Failover kind, Action<During>? during = null)
        {
            if (during is not null && kind != Failover.Graceful)
            {
                throw new ArgumentException($"A {kind} failover has no rebalance to run steps during.", nameof(during));
            }
            var resolved = Hosts(hosts);
            return Add(new ClusterStep($"FailOver {List(resolved)} {kind}",
                m => ModelSequences.FailOverHosts(m, m.Buckets, resolved, kind, m.Seed), Points(during)));
        }

        /// <summary>
        /// A failed host comes back, and every bucket rebalances. Read <see cref="Recovery"/> for the kinds.
        /// </summary>
        public TSelf Recover(string host, Recovery kind)
        {
            var resolved = Hostname(host);
            return Add(new ClusterStep($"Recover {resolved} {kind}", m => ModelSequences.Recover(m, m.Buckets, resolved, kind, m.Seed), null));
        }

        /// <summary>
        /// An ejected host joins again as a new member, outside every bucket, with no rebalance.
        /// </summary>
        /// <param name="services">The default services of the profile when null.</param>
        public TSelf Rejoin(string host, IReadOnlyList<string>? services = null)
        {
            var e = new RejoinNode(Hostname(host), services);
            return Add(new ClusterStep(e.ToString(), _ => [e], null));
        }

        public TSelf SetServices(string host, IReadOnlyList<string> services)
        {
            var e = new SetServices(Hostname(host), services);
            return Add(new ClusterStep(e.ToString(), _ => [e], null));
        }

        /// <summary>
        /// The host holds every reply and notification, like a paused process. Real mode only.
        /// </summary>
        public TSelf Hang(string host) => AddNetwork(nameof(Hang), host, (server, h) => server.Hang(h));

        /// <summary>
        /// The host stops reading, so SDK writes wait once its buffer is full. Real mode only.
        /// </summary>
        public TSelf Stall(string host) => AddNetwork(nameof(Stall), host, (server, h) => server.Stall(h));

        /// <summary>
        /// Ends a hang or a stall. The host sends what it held. Real mode only.
        /// </summary>
        public TSelf Resume(string host) => AddNetwork(nameof(Resume), host, (server, h) => server.Resume(h));

        /// <summary>
        /// The host ends every connection to it. Real mode only.
        /// </summary>
        public TSelf Close(string host) => AddNetwork(nameof(Close), host, (server, h) => server.Close(h));

        /// <summary>
        /// While on, the host refuses new connections. Open connections stay open. Real mode only.
        /// </summary>
        public TSelf Refuse(string host, bool refuse) =>
            AddNetwork(refuse ? "Refuse" : "Accept", host, (server, h) => server.Refuse(h, refuse));

        /// <summary>
        /// The host gets no new maps until <see cref="CatchUp"/>, so it keeps serving the versions it has.
        /// </summary>
        public TSelf Lag(string host) => Add(new LagStep(Hostname(host)));

        /// <summary>
        /// The host gets the latest maps and takes every delivery again.
        /// </summary>
        public TSelf CatchUp(string host) => Add(new CatchUpStep(Hostname(host)));

        /// <summary>
        /// The next deliveries only serve the maps, so the SDK finds them only when it fetches.
        /// </summary>
        public TSelf NoPush() => Add(new PushStep(Notify: false));

        /// <summary>
        /// The next deliveries notify the connections again. This is the default.
        /// </summary>
        public TSelf Push() => Add(new PushStep(Notify: true));

        /// <summary>
        /// Moves the clock forward, or waits on real time when the scenario runs on the system clock.
        /// </summary>
        public TSelf Wait(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration), duration, "Time cannot go back.");
            return Add(new WaitStep(duration));
        }

        /// <summary>
        /// Runs test code, for example to send KV operations or to assert mid run.
        /// </summary>
        public TSelf Do(Func<ScenarioContext, Task> action) => Add(new DoStep(action));

        public TSelf Do(Action<ScenarioContext> action) => Do(context =>
        {
            action(context);
            return Task.CompletedTask;
        });

        /// <summary>
        /// Runs the steps with no check after them.
        /// </summary>
        public TSelf Unchecked(Action<ScenarioSteps> steps)
        {
            var nested = new ScenarioSteps(Mode);
            steps(nested);
            return Add(new UncheckedStep(nested.TakeSteps()));
        }

        /// <summary>
        /// Returns the steps added so far and starts a new list.
        /// </summary>
        internal IReadOnlyList<ScenarioStep> TakeSteps()
        {
            var steps = _steps.ToList();
            _steps.Clear();
            return steps;
        }

        private TSelf AddScaleIn(IReadOnlyList<string> hosts, Action<During>? during)
        {
            if (hosts.Count == 0) throw new ArgumentException("A scale in removes at least one host.", nameof(hosts));
            var resolved = Hosts(hosts);
            return Add(new ClusterStep($"ScaleIn {List(resolved)}", m => ModelSequences.ScaleIn(m, m.Buckets, resolved, m.Seed), Points(during)));
        }

        private TSelf AddNetwork(string name, string host, Action<FakeClusterServer, string> apply)
        {
            var step = new NetworkStep(name, Hostname(host), apply);
            if (Mode != ConnectionMode.Real)
            {
                throw new InvalidOperationException(
                    $"{step} needs real connections. Call {nameof(ScenarioSetup.RealConnections)} in the scenario setup.");
            }
            return Add(step);
        }

        private DuringSteps? Points(Action<During>? during)
        {
            if (during is null) return null;
            var points = new During(Mode);
            during(points);
            return points.Build();
        }

        private TSelf Add(ScenarioStep step)
        {
            _steps.Add(step);
            return (TSelf)this;
        }

        private static string Hostname(string host) => ScenarioContext.Hostname(host);

        private static IReadOnlyList<string> Hosts(IReadOnlyList<string> hosts) => hosts.Select(Hostname).ToList();

        private static string List(IEnumerable<string> values) => $"[{string.Join(", ", values)}]";
    }

    /// <summary>
    /// The steps of a during point or of <see cref="ScenarioBuilder{TSelf}.Unchecked"/>.
    /// </summary>
    internal sealed class ScenarioSteps : ScenarioBuilder<ScenarioSteps>
    {
        internal ScenarioSteps(ConnectionMode mode) : base(mode)
        {
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
