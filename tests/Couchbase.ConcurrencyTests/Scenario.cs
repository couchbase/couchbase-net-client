using Couchbase.ConcurrencyTests.Actors;
using Couchbase.ConcurrencyTests.Connections;
using Couchbase.KeyValue;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.ConcurrencyTests
{
    internal class Scenario : IAsyncDisposable
    {
        private readonly CancellationTokenSource internalCancellation = new();
        private readonly IReadOnlyCollection<Actor> actors;

        public string Name { get; }
        public int ActorCount => actors.Count;

        public Scenario(string name, IEnumerable<Actor> actors)
        {
            this.actors = actors.ToList();
            Name = name;
        }

        public virtual async Task Warmup(CancellationToken cancellationToken)
        {
            foreach (var actor in actors)
            {
                await actor.Warmup(cancellationToken);
            }
        }
        public async Task Run(CancellationToken cancellationToken)
        {
            var runTasks = new ConcurrentBag<Task>();
            var linkedCancel = CancellationTokenSource.CreateLinkedTokenSource(this.internalCancellation.Token, cancellationToken);
            foreach (var actor in this.actors)
            {
                var t = actor.Run(linkedCancel.Token);
                runTasks.Add(t);
            }

            await Task.WhenAll(runTasks);
        }

        public virtual async Task Cleanup(CancellationToken cancellationToken)
        {
            Serilog.Log.Information("Cleaning up Scenario: {Name}", Name);
            this.internalCancellation.Cancel();
            foreach (var actor in actors)
            {
                await actor.Cleanup(cancellationToken);
            }
        }

        public virtual async ValueTask DisposeAsync()
        {
            await Cleanup(CancellationToken.None);
            foreach (var actor in actors)
            {
                await actor.DisposeAsync();
            }

            foreach (var actor in actors)
            {
                if (actor.Status.HasFlag(Actor.RunStatus.Faulted))
                {
                    Serilog.Log.Warning("[{aid}] {Actor}: {Status}", actor.ActorId, actor.ActorName, actor.Status);
                }
            }
        }
    }

    internal static class Scenarios
    {
        internal static IEnumerable<Scenario> GetScenarios(string connectionId, IEnumerable<string> scenarioDefs, Func<int, TimeSpan>? delayBetweenCycles = null)
        {
            foreach (var scenario in scenarioDefs)
            {
                var pieces = scenario.Split(',');
                var scenarioName = pieces[0];
                int numActors = pieces.Length > 1 ? int.Parse(pieces[1]) : 50;
                switch (scenarioName)
                {
                    case "ping":
                        yield return PingScenario(connectionId, numActors, delayBetweenCycles);
                        break;
                    case "getDocument":
                        yield return GetDocumentScenario(connectionId, numActors, delayBetweenCycles);
                        break;
                    case "crud":
                        yield return CrudScenario(connectionId, numActors, delayBetweenCycles);
                        break;
                    default:
                        Serilog.Log.Warning("Unrecognized scenario: {name}", scenarioName);
                        break;
                }
            }
        }

        internal static Scenario PingScenario(string connectionId, int numActors, Func<int, TimeSpan>? delayBetweenCycles = null)
        {
            delayBetweenCycles ??= i => TimeSpan.Zero;
            var pingActors = from i in Enumerable.Range(0, numActors)
                             select new PingActor(connectionId, delayBetweenCycles(i));
            return new Scenario("Ping", pingActors);
        }

        internal static Scenario GetDocumentScenario(string connectionId, int numActors, Func<int, TimeSpan>? delayBetweenCycles = null)
        {
            delayBetweenCycles ??= i => TimeSpan.Zero;
            var actors = from i in Enumerable.Range(0, numActors)
                         select new GetDocumentActor(connectionId, delayBetweenCycles(i));
            return new Scenario("GetDocument", actors);
        }

        internal static Scenario CrudScenario(string connectionId, int numActors, Func<int, TimeSpan>? delayBetweenCycles = null)
        {
            delayBetweenCycles ??= i => TimeSpan.Zero;
            var actors = from i in Enumerable.Range(0, numActors)
                         select new CrudActor(connectionId, delayBetweenCycles(i));
            return new Scenario("CRUD", actors);
        }
    }
} 
