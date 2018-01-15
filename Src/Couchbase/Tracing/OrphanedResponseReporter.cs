using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Logging;
using Newtonsoft.Json;

namespace Couchbase.Tracing
{
    internal class OrphanedResponseReporter : IOrphanedOperationReporter
    {
        private static readonly ILog Log = LogManager.GetLogger<OrphanedResponseReporter>();

        private readonly CancellationTokenSource _source = new CancellationTokenSource();
        private readonly ConcurrentQueue<OrphanedResponseSummary> _queuedTrophies = new ConcurrentQueue<OrphanedResponseSummary>();

        internal int Interval { get; }
        internal int SampleSize { get; }

        internal OrphanedResponseReporter(int interval, int sampleSize)
        {
            Interval = interval;
            SampleSize = sampleSize;

            Task.Factory.StartNew(DoWork, TaskCreationOptions.LongRunning);
        }

        internal OrphanedResponseReporter()
        {
            Interval = 10000; // 10 seconds
            SampleSize = 10;

            Task.Factory.StartNew(DoWork, TaskCreationOptions.LongRunning);
        }

        private async Task DoWork()
        {
            while (!_source.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Interval), _source.Token);
                if (!_queuedTrophies.Any())
                {
                    continue;
                }

                var trophies = new List<OrphanedResponseSummary>();
                while (_queuedTrophies.TryDequeue(out var orphan))
                {
                    trophies.Add(orphan);
                }

                var result = new
                {
                    count = trophies.Count,
                    top = trophies
                        .OrderByDescending(x => x.ServerDuration)
                        .Take(SampleSize)
                };

                Log.Warn("Orphaned responses observed: {0}", JsonConvert.SerializeObject(result, Formatting.None));
            }
        }

        public void Add(string endpoint, string correlationId, long? serverDuration)
        {
            _queuedTrophies.Enqueue(new OrphanedResponseSummary(endpoint, correlationId, serverDuration));
        }

        public void Dispose()
        {
            _source?.Cancel();
        }

        private class OrphanedResponseSummary
        {
            [JsonProperty("endpoint")]
            public string Endpoint { get; }

            [JsonProperty("correlation_id")]
            public string CorrelationId { get; }

            [JsonProperty("server_duration_us")]
            public long? ServerDuration { get; }

            public OrphanedResponseSummary(string endpoint, string correlationId, long? serverDuration)
            {
                Endpoint = endpoint;
                CorrelationId = correlationId;
                ServerDuration = serverDuration;
            }
        }
    }
}
