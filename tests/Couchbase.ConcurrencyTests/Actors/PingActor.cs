using Couchbase.ConcurrencyTests.Connections;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.ConcurrencyTests.Actors
{
    internal class PingActor : Actor
    {
        private static long PingActorsRunning = 0;
        private static readonly Counter<long> RunCycles = Metrics.TestMeter.CreateCounter<long>(
            name: Metrics.CounterName(nameof(PingActor)),
            unit: "Runs",
            description: "Number of runs");
        private static readonly ObservableGauge<long> PingActorsRunningGauge = Metrics.TestMeter.CreateObservableGauge<long>(
                name: Metrics.CounterName(nameof(PingActor) + ".Running"),
                observeValue: () => new Measurement<long>(Interlocked.Read(ref PingActorsRunning)),
                description: "Actors running"
                );
        private readonly string connectionId;
        private readonly TimeSpan delayBetweenCycles;

        protected override Counter<long> RunCyclesCounter => RunCycles;

        public override string ActorName => nameof(PingActor);


        public PingActor(string connectionId, TimeSpan delayBetweenCycles)
        {
            this.connectionId = connectionId;
            this.delayBetweenCycles = delayBetweenCycles;
        }

        protected override async Task RunInternal(CancellationToken cancellationToken)
        {
            var cluster = ConnectionManager.GetCluster(this.connectionId);
            while (!cancellationToken.IsCancellationRequested)
            {
                var pingReport = await cluster.PingAsync(new Diagnostics.PingOptions()
                    .CancellationToken(cancellationToken)
                    .ServiceTypes(new[] { ServiceType.KeyValue })
                    );
                await Task.Delay(this.delayBetweenCycles, cancellationToken);
            }
        }

        public override async Task Cleanup(CancellationToken cancellationToken)
        {
            this.internalCancellation.Cancel();
            await base.Cleanup(cancellationToken);
            Serilog.Log.Information("[{aid}] Ping Actor stopped.", this.ActorId);
        }

        protected override long IncrementActorsRunning() => Interlocked.Increment(ref PingActorsRunning);

        protected override long DecrementActorsRunning() => Interlocked.Decrement(ref PingActorsRunning);
    }
}
