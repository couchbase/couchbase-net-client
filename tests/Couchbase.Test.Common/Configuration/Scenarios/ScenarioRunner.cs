#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Test.Common.Configuration.Model;
using Couchbase.Test.Common.Utils;

namespace Couchbase.Test.Common.Configuration.Scenarios
{
    /// <summary>
    /// Runs the steps of a <see cref="Scenario"/> in order, and writes the step log.
    /// </summary>
    /// <remarks>
    /// After each model event every node that does not lag gets the latest maps. A check follows each model event,
    /// each <see cref="WaitStep"/> and each <see cref="CatchUpStep"/>, except inside <see cref="UncheckedStep"/>.
    /// The check after an event that only changes the rev or the epoch waits for the SDK and compares nothing (bug 6).
    /// A node that leaves the cluster ends its connections before its delivery, as <see cref="ModelServer.Leave"/> says.
    /// In real mode on the virtual clock the runner then settles before the delivery, so the SDK has time to reconnect and
    /// get the select bucket failure before the map without the node. On real time the two race, as in the captures.
    /// </remarks>
    internal sealed class ScenarioRunner(ScenarioContext context, ScenarioCheck check)
    {
        private readonly HashSet<string> _lagging = new(StringComparer.Ordinal);
        private readonly List<string> _log = [];
        private List<string> _differences = [];
        private bool _push = true;

        /// <summary>
        /// One line per step since the scenario started, and one per model event with its rev and the maps it changed.
        /// The lines of nested steps are indented.
        /// </summary>
        public IReadOnlyList<string> Log => _log;

        /// <returns>The differences that the checks of these steps found.</returns>
        public async Task<IReadOnlyList<string>> RunAsync(IReadOnlyList<ScenarioStep> steps)
        {
            _differences = [];
            foreach (var step in steps)
            {
                await RunAsync(step, depth: 0, checking: true).ConfigureAwait(false);
            }
            return _differences;
        }

        private async Task RunAsync(ScenarioStep step, int depth, bool checking)
        {
            Write(depth, step.ToString());
            switch (step)
            {
                case ClusterStep cluster:
                    await RunClusterAsync(cluster, depth, checking).ConfigureAwait(false);
                    break;
                case NetworkStep network:
                    network.Apply(context.Harness.Server, network.Host);
                    break;
                case LagStep lag:
                    _lagging.Add(lag.Host);
                    break;
                case CatchUpStep catchUp:
                    _lagging.Remove(catchUp.Host);
                    await LeaveAsync(step.ToString()).ConfigureAwait(false);
                    context.Server.Deliver(catchUp.Host, _push);
                    if (checking) await CheckAsync(step.ToString(), compare: true).ConfigureAwait(false);
                    break;
                case PushStep push:
                    _push = push.Notify;
                    break;
                case WaitStep wait:
                    await check.WaitAsync(wait.Duration, step.ToString(), _differences).ConfigureAwait(false);
                    if (checking) await CheckAsync(step.ToString(), compare: true).ConfigureAwait(false);
                    break;
                case DoStep action:
                    await action.Action(context).ConfigureAwait(false);
                    break;
                case UncheckedStep quiet:
                    foreach (var nested in quiet.Steps)
                    {
                        await RunAsync(nested, depth + 1, checking: false).ConfigureAwait(false);
                    }
                    break;
                default:
                    throw new ArgumentException($"The runner has no rule for {step.GetType().Name}.", nameof(step));
            }
        }

        private async Task RunClusterAsync(ClusterStep step, int depth, bool checking)
        {
            var model = context.Model;
            var points = step.During is null ? null : new Points(step.During);
            foreach (var e in step.Events(model))
            {
                if (points is not null && points.Finishes(e))
                {
                    await RunPointAsync("before finish", points.Steps.BeforeFinish, depth, checking).ConfigureAwait(false);
                }

                var changed = model.Apply(e);
                Write(depth + 1, $"{model.Applied[model.Applied.Count - 1].Rev} {e} -> {string.Join(", ", changed.Select(m => m.Bucket ?? "cluster"))}");
                check.Applied(e, changed);
                await LeaveAsync($"{e} in {step}").ConfigureAwait(false);
                context.Server.DeliverAll(_push, _lagging);
                if (checking)
                {
                    var after = step.Name == e.ToString() ? step.Name : $"{e} in {step}";
                    await CheckAsync(after, compare: !ScenarioCheck.IsRevOnly(e)).ConfigureAwait(false);
                }

                if (points is null) continue;
                if (points.Starts(e, model))
                {
                    await RunPointAsync("after start", points.Steps.AfterStart, depth, checking).ConfigureAwait(false);
                }
                if (points.ReachesHalfway(e, model))
                {
                    await RunPointAsync("halfway", points.Steps.Halfway, depth, checking).ConfigureAwait(false);
                }
            }

            if (points is not null && points.Missed() is { } missed)
            {
                throw new InvalidOperationException($"{step} did not reach its {missed} point. A point needs a rebalance in the step.");
            }
        }

        private async Task RunPointAsync(string name, IReadOnlyList<ScenarioStep> steps, int depth, bool checking)
        {
            if (steps.Count == 0) return;

            Write(depth + 1, $"at {name}");
            foreach (var step in steps)
            {
                await RunAsync(step, depth + 2, checking).ConfigureAwait(false);
            }
        }

        private async Task LeaveAsync(string during)
        {
            var harness = context.Harness;
            if (context.Server.Leave(_lagging).Count == 0 || !harness.Server.HasNetwork || harness.Clock is not SteppableClock) return;

            try
            {
                await harness.SettleAsync().ConfigureAwait(false);
            }
            catch (TimeoutException e)
            {
                _differences.Add($"After the leave in {during}, {e.Message}");
            }
        }

        private async Task CheckAsync(string after, bool compare)
        {
            _differences.AddRange(await check.CheckAsync(after, _push, compare).ConfigureAwait(false));
        }

        private void Write(int depth, string line) => _log.Add(new string(' ', depth * 2) + line);

        /// <summary>
        /// Follows the first rebalance of a cluster step, to tell when each during point is due.
        /// </summary>
        private sealed class Points(DuringSteps steps)
        {
            private string? _bucket;
            private int _start;
            private bool _halfway;
            private bool _finished;

            public DuringSteps Steps { get; } = steps;

            /// <summary>
            /// True once, for the first event that sets a forward map.
            /// </summary>
            public bool Starts(ModelEvent e, ClusterModel model)
            {
                if (_bucket is not null) return false;
                _bucket = e switch
                {
                    StartRebalance start => start.Bucket,
                    StartGracefulFailover start => start.Bucket,
                    _ => null
                };
                if (_bucket is null) return false;

                _start = model.PendingMoves(_bucket).Count;
                return true;
            }

            /// <summary>
            /// True once, for the first event of the rebalance after which half of the moves at start, or less, are pending.
            /// </summary>
            public bool ReachesHalfway(ModelEvent e, ClusterModel model)
            {
                if (_halfway || _bucket is null || _finished) return false;
                if (e is not (StartRebalance or StartGracefulFailover or MoveVBuckets)) return false;
                if (model.PendingMoves(_bucket).Count * 2 > _start) return false;

                _halfway = true;
                return true;
            }

            /// <summary>
            /// True once, for the event that clears the forward map of the rebalance.
            /// </summary>
            public bool Finishes(ModelEvent e)
            {
                if (_finished || _bucket is null || e is not FinishRebalance finish || finish.Bucket != _bucket) return false;

                _finished = true;
                return true;
            }

            /// <returns>The first point that did not run, or null.</returns>
            public string? Missed() => _bucket is null ? "after start" : !_halfway ? "halfway" : !_finished ? "before finish" : null;
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
