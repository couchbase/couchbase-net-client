#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Sharding;
using Couchbase.Test.Common.Configuration.Model;
using Couchbase.Test.Common.Utils;
using Xunit;

namespace Couchbase.Test.Common.Configuration.Scenarios
{
    /// <summary>
    /// Waits for the SDK to reach the latest map of each bucket, then lists how the SDK buckets differ from the model.
    /// It compares the map version, the bucket nodes and the active node of each vbucket.
    /// </summary>
    /// <remarks>
    /// The wait ends once the handler dispatched the latest version of every bucket, or the bucket has it. Settling
    /// alone can return while a fetched map is still on its way to the bucket, because the fetch counts as activity
    /// when it is sent.
    /// The SDK does not take a version that only changes the rev or the epoch (bug 6). So a bucket whose latest
    /// versions are of that kind can also be on the version before them.
    /// A bucket that misses the deadline gets no new wait until the SDK has its latest version again. So a stuck SDK
    /// costs one deadline, not one per step.
    /// </remarks>
    internal sealed class ScenarioCheck(ScenarioContext context, TimeSpan deadline)
    {
        private static readonly TimeSpan PollStep = TimeSpan.FromMilliseconds(5);

        private readonly HashSet<(string Bucket, ConfigVersion Version)> _revOnly = [];
        private readonly HashSet<string> _stuck = new(StringComparer.Ordinal);

        public TimeSpan Deadline { get; } = deadline;

        /// <summary>
        /// True for the events that give versions the SDK does not take.
        /// </summary>
        public static bool IsRevOnly(ModelEvent e) => e is BumpRev or BumpAll or BumpEpoch;

        /// <summary>
        /// Notes the maps of an event the model applied.
        /// </summary>
        public void Applied(ModelEvent e, IReadOnlyList<ModelMap> changed)
        {
            if (!IsRevOnly(e)) return;
            foreach (var map in changed.Where(m => m.Bucket is not null))
            {
                _revOnly.Add((map.Bucket!, map.Version));
            }
        }

        /// <summary>
        /// Waits for the SDK and lists the differences.
        /// </summary>
        /// <param name="after">What the step log calls the last change, for the messages.</param>
        /// <param name="push">False when the last deliveries did not notify, so the SDK only finds them by polling.</param>
        /// <param name="compare">False to only wait.</param>
        public async Task<List<string>> CheckAsync(string after, bool push, bool compare)
        {
            var differences = new List<string>();
            _stuck.IntersectWith(Late());
            var stuck = _stuck.ToList();
            var late = await WaitForBucketsAsync(push, after, differences).ConfigureAwait(false);
            foreach (var bucket in late)
            {
                var wait = stuck.Contains(bucket) ? "with no new wait after an earlier miss" : $"in {Deadline.TotalSeconds} s";
                differences.Add($"After {after}, the SDK did not get {bucket} {context.Model.Bucket(bucket).Version} {wait}. " +
                                $"It has {context.View(bucket).Version}.");
            }
            _stuck.UnionWith(late);

            await QuietAsync(after, differences).ConfigureAwait(false);
            if (!compare) return differences;

            foreach (var bucket in context.Model.Buckets.Where(b => !late.Contains(b)))
            {
                Compare(bucket, after, differences);
            }
            return differences;
        }

        /// <summary>
        /// Moves the clock, or waits on real time when the scenario runs on the system clock.
        /// </summary>
        public async Task WaitAsync(TimeSpan duration, string after, List<string> differences)
        {
            if (context.Harness.Clock is not SteppableClock)
            {
                await Task.Delay(duration).ConfigureAwait(false);
                return;
            }

            try
            {
                await context.Harness.AdvanceAsync(duration).ConfigureAwait(false);
            }
            catch (TimeoutException e)
            {
                differences.Add($"After {after}, {e.Message}");
            }
        }

        /// <summary>
        /// The vbuckets the SDK maps to another active host than the model. A vbucket with no copy in the model is skipped.
        /// </summary>
        public static List<int> WrongActives(CouchbaseBucket bucket, ModelMap map)
        {
            var keyMapper = Assert.IsType<VBucketKeyMapper>(bucket.KeyMapper);
            return Enumerable.Range(0, map.VBucketCount)
                .Where(v => map.ActiveHost(v) is { } active && keyMapper[(short)v].LocatePrimary()?.Host != active)
                .ToList();
        }

        /// <returns>The buckets whose latest version the handler did not dispatch in time.</returns>
        private async Task<List<string>> WaitForBucketsAsync(bool push, string after, List<string> differences)
        {
            var harness = context.Harness;
            if (!push && harness.Clock is SteppableClock)
            {
                // With no push the SDK finds a map only on a poll pass, so the clock moves one poll interval at a time.
                var interval = harness.Options.ConfigPollInterval;
                for (var waited = TimeSpan.Zero; Waiting() && waited < Deadline; waited += interval)
                {
                    await WaitAsync(interval, after, differences).ConfigureAwait(false);
                }
                return Late();
            }

            var elapsed = Stopwatch.StartNew();
            while (Waiting() && elapsed.Elapsed < Deadline)
            {
                await Task.Delay(PollStep).ConfigureAwait(false);
            }
            return Late();
        }

        private bool Waiting() => Late().Any(b => !_stuck.Contains(b));

        // The first version of a bucket comes from its bootstrap, so the handler never dispatches it.
        private List<string> Late() => context.Model.Buckets.Where(b =>
        {
            var latest = context.Model.Bucket(b).Version;
            return !(context.Bucket(b).CurrentConfig?.ConfigVersion >= latest) && !context.Harness.HasDispatched(b, latest);
        }).ToList();

        // Lets every subscriber process the dispatched maps, and on the virtual clock lets the pipeline go quiet.
        private async Task QuietAsync(string after, List<string> differences)
        {
            var harness = context.Harness;
            using var cts = new CancellationTokenSource();
            var drained = harness.DrainAsync();
            var completed = await Task.WhenAny(drained, Task.Delay(Deadline, cts.Token)).ConfigureAwait(false);
            cts.Cancel();
            if (completed != drained)
            {
                differences.Add($"After {after}, the config handler did not drain in {Deadline.TotalSeconds} s.");
                return;
            }

            if (harness.Clock is not SteppableClock) return;
            try
            {
                await harness.SettleAsync().ConfigureAwait(false);
            }
            catch (TimeoutException e)
            {
                differences.Add($"After {after}, {e.Message}");
            }
        }

        private void Compare(string bucket, string after, List<string> differences)
        {
            var latest = context.Model.Bucket(bucket);
            var view = context.View(bucket);
            if (view.Version is not { } version || !Accepted(bucket).Contains(version))
            {
                differences.Add($"After {after}, the SDK has {bucket} {view.Version} and the model has {latest.Version}.");
            }

            var nodes = latest.BucketNodes.OrderBy(h => h, StringComparer.Ordinal).ToList();
            if (!view.Nodes.SequenceEqual(nodes))
            {
                differences.Add($"After {after}, the SDK nodes of {bucket} are [{string.Join(", ", view.Nodes)}] " +
                                $"and the model nodes are [{string.Join(", ", nodes)}].");
            }

            if (WrongActives(context.Bucket(bucket), latest) is { Count: > 0 } wrong)
            {
                differences.Add($"After {after}, the SDK maps {wrong.Count} vbuckets of {bucket} to another active node, " +
                                $"the first is vbucket {wrong[0]}.");
            }
        }

        // The latest version, and when it only changes the rev or the epoch, each version back to the last real change.
        private List<ConfigVersion> Accepted(string bucket)
        {
            var accepted = new List<ConfigVersion>();
            var history = context.Model.History(bucket);
            for (var i = history.Count - 1; i >= 0; i--)
            {
                accepted.Add(history[i].Version);
                if (!_revOnly.Contains((bucket, history[i].Version))) break;
            }
            return accepted;
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
