using Couchbase.ConcurrencyTests.Connections;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.KeyValue;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.ConcurrencyTests.Actors
{
    internal class GetDocumentActor : Actor
    {
        private static long GetDocumentActorsRunning = 0;
        private static readonly Counter<long> RunCycles = Metrics.TestMeter.CreateCounter<long>(
            name: Metrics.CounterName(nameof(GetDocumentActor)),
            unit: "Runs",
            description: "Number of runs");
        private static readonly ObservableGauge<long> PingActorsRunningGauge = Metrics.TestMeter.CreateObservableGauge<long>(
                name: Metrics.CounterName(nameof(GetDocumentActor) + ".Running"),
                observeValue: () => new Measurement<long>(Interlocked.Read(ref GetDocumentActorsRunning)),
                description: "Actors running"
                );
        private readonly string connectionId;
        private readonly TimeSpan delayBetweenCycles;
        private readonly string docId;

        protected override Counter<long> RunCyclesCounter => RunCycles;

        public override string ActorName => nameof(GetDocumentActor);

        public record GetDocumentActorDoc(string docId, long actorId, string actorName = nameof(GetDocumentActor));

        public GetDocumentActor(string connectionId, TimeSpan delayBetweenCycles, string? docId = null)
        {
            this.connectionId = connectionId;
            this.delayBetweenCycles = delayBetweenCycles;
            this.docId = docId ?? Guid.NewGuid().ToString();
        }

        public override async Task Warmup(CancellationToken cancellationToken)
        {
            await base.Warmup(cancellationToken);
            var cluster = ConnectionManager.GetCluster(connectionId);
            var defaultBucket = await cluster.BucketAsync("default");
            var defaultCollection = defaultBucket.DefaultCollection();
            _ = await defaultCollection.InsertAsync(this.docId, new GetDocumentActorDoc(this.docId, this.ActorId),
                opts => opts.CancellationToken(cancellationToken));
        }
        protected override async Task RunInternal(CancellationToken cancellationToken)
        {
            var cluster = ConnectionManager.GetCluster(connectionId);
            var defaultBucket = await cluster.BucketAsync("default");
            var defaultCollection = defaultBucket.DefaultCollection();
            while (!cancellationToken.IsCancellationRequested)
            {
                var getResult = await defaultCollection.GetAsync(this.docId, opts => opts.CancellationToken(cancellationToken));
                var doc = getResult.ContentAs<GetDocumentActorDoc>();
                if (doc?.actorId != this.ActorId
                    || doc?.actorName != nameof(GetDocumentActor))
                {
                    Serilog.Log.Error("Expected ({actorId},{actorName}), Got {doc} ", this.ActorId, nameof(GetDocumentActor));
                    throw new InvalidOperationException("Retrieved the wrong document!");
                }

                await Task.Delay(this.delayBetweenCycles, cancellationToken);
            }
        }

        public override async Task Cleanup(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Serilog.Log.Warning("Token is already cancelled in cleanup.");
            }

            var cluster = ConnectionManager.GetCluster(connectionId);
            var defaultBucket = await cluster.BucketAsync("default");
            var defaultCollection = defaultBucket.DefaultCollection();
            try
            {

                await defaultCollection.RemoveAsync(this.docId,
                    opts => opts.CancellationToken(cancellationToken).Cas(0));
            }
            catch (DocumentNotFoundException)
            {
                Serilog.Log.Warning("[{aid}] {ActorName}: document not found during cleanup: {docId}", this.ActorId, this.ActorName, this.docId);
            }

            await base.Cleanup(cancellationToken);
        }

        protected override long IncrementActorsRunning() => Interlocked.Increment(ref GetDocumentActorsRunning);

        protected override long DecrementActorsRunning() => Interlocked.Decrement(ref GetDocumentActorsRunning);
    }
}
