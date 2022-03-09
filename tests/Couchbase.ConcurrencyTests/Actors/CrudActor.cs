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
    internal class CrudActor : Actor
    {
        private static long CrudActorsRunning = 0;
        private static readonly Counter<long> RunCycles = Metrics.TestMeter.CreateCounter<long>(
            name: Metrics.CounterName(nameof(CrudActor)),
            unit: "Runs",
            description: "Number of runs");
        private static readonly ObservableGauge<long> PingActorsRunningGauge = Metrics.TestMeter.CreateObservableGauge<long>(
                name: Metrics.CounterName(nameof(CrudActor) + ".Running"),
                observeValue: () => new Measurement<long>(Interlocked.Read(ref CrudActorsRunning)),
                description: "Actors running"
                );
        private readonly string connectionId;
        private readonly TimeSpan delayBetweenCycles;
        private readonly string docId;

        protected override Counter<long> RunCyclesCounter => RunCycles;

        public override string ActorName => nameof(CrudActor);

        public CrudActor(string connectionId, TimeSpan delayBetweenCycles, string? docId = null)
        {
            this.connectionId = connectionId;
            this.delayBetweenCycles = delayBetweenCycles;
            this.docId = docId ?? Guid.NewGuid().ToString();
        }

        protected override long IncrementActorsRunning() => Interlocked.Increment(ref CrudActorsRunning);
        protected override long DecrementActorsRunning() => Interlocked.Decrement(ref CrudActorsRunning);


        public record CrudActorDoc(string docId, long actorId, long? updateCount = 0, string actorName = nameof(CrudActor));

        protected override async Task RunInternal(CancellationToken cancellationToken)
        {
            var cluster = ConnectionManager.GetCluster(this.connectionId);
            var defaultBucket = await cluster.BucketAsync("default");
            var defaultCollection = defaultBucket.DefaultCollection();
            while (!cancellationToken.IsCancellationRequested)
            {
                var doc = new CrudActorDoc(this.docId, this.ActorId);
                var insertResult = await defaultCollection.InsertAsync(this.docId, doc, opts => opts.CancellationToken(cancellationToken));
                var getResult = await defaultCollection.GetAsync(this.docId, opts => opts.CancellationToken(cancellationToken));
                var docRoundTrip = getResult.ContentAs<CrudActorDoc>();
                if (docRoundTrip != doc)
                {
                    Serilog.Log.Error("Expected ({actorId},{actorName}), Got {doc} ", this.ActorId, nameof(GetDocumentActor));
                    throw new InvalidOperationException("Retrieved the wrong document!");
                }

                var upsertResult = await defaultCollection.UpsertAsync(this.docId, docRoundTrip! with { updateCount = docRoundTrip.updateCount + 1 },
                    opts => opts.CancellationToken(cancellationToken));

                if (upsertResult.Cas == 0)
                {
                    Serilog.Log.Error("Upsert failed!");
                    throw new InvalidOperationException("Upsert failed");
                }

                var replaceResult = await defaultCollection.ReplaceAsync(this.docId, docRoundTrip! with { updateCount = docRoundTrip.updateCount + 2 },
                    opts => opts.CancellationToken(cancellationToken).Cas(upsertResult.Cas));

                if (replaceResult.Cas == 0)
                {
                    Serilog.Log.Error("Replace failed!");
                    throw new InvalidOperationException("Replace failed");
                }

                await defaultCollection.RemoveAsync(this.docId,
                    opts => opts.CancellationToken(cancellationToken).Cas(replaceResult.Cas));

                await Task.Delay(this.delayBetweenCycles, cancellationToken);
            }
        }

        public override async Task Cleanup(CancellationToken cancellationToken)
        {
            var cluster = ConnectionManager.GetCluster(connectionId);
            var defaultBucket = await cluster.BucketAsync("default");
            var defaultCollection = defaultBucket.DefaultCollection();
            try
            {
                await defaultCollection.RemoveAsync(this.docId,
                    opts => opts.CancellationToken(cancellationToken));
            }
            catch (DocumentNotFoundException)
            {
                // expected
            }

            await base.Cleanup(cancellationToken);
        }
    }
}
