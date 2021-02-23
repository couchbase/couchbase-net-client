using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Couchbase.Core.Diagnostics.Tracing.ThresholdTracing
{
    internal class ThresholdServiceQueue
    {
        private int _sampleSize = ThresholdOptions.DefaultSampleSize;
        private long _sampleCount = 0;
        private readonly ConcurrentQueue<ThresholdSummary> _latestEvents = new();

        private static readonly ManualResetEventSlim ReportIdle = new(true);

        internal static readonly IReadOnlyDictionary<string, ThresholdServiceQueue> CoreQueues =
            ServiceIdentifier.CoreServices.Select(s => new ThresholdServiceQueue(s))
                .ToDictionary(sq => sq.ServiceName);

        internal ThresholdServiceQueue(string serviceName)
        {
            ServiceName = serviceName;
        }

        internal string ServiceName { get; }

        private void Add(ThresholdSummary overThresholdEvent)
        {
            // Don't add while sample reporting is being done.
            // We don't care otherwise if Add races a little against other adds.
            ReportIdle.Wait();

            Interlocked.Increment(ref _sampleCount);
            _latestEvents.Enqueue(overThresholdEvent);
            if (_latestEvents.Count > _sampleSize)
            {
                _latestEvents.TryDequeue(out var oldest);
            }
        }

        public static bool AddByService(string serviceName, ThresholdSummary overThresholdSummary)
        {
            if (CoreQueues.TryGetValue(serviceName, out var serviceQueue))
            {
                serviceQueue.Add(overThresholdSummary);
                return true;
            }

            return false;
        }

        private ThresholdSummaryReport ReportAndReset()
        {
            // to avoid a race, this should only be called while waiting on ReportMutex.
            var count = Interlocked.Exchange(ref _sampleCount, 0);

            var sanity = 0;
            while (_latestEvents.Count > _sampleSize)
            {
                // throw away any extras at the front.
                _latestEvents.TryDequeue(out _);

                if (sanity++ > 10000)
                {
                    throw new InvalidOperationException("Possible infinite loop detected.");
                }
            }

            var top = _latestEvents.ToArray();

            while (_latestEvents.TryDequeue(out _))
            {
                // just clearing the queue.
                if (sanity++ > 10000)
                {
                    throw new InvalidOperationException("Possible infinite loop detected.");
                }
            }

            return new ThresholdSummaryReport(ServiceName, (int)count, top);
        }

        internal static void SetSampleSize(int sampleSize)
        {
            foreach (var cq in CoreQueues)
            {
                cq.Value._sampleSize = sampleSize;
            }
        }

        public static IEnumerable<ThresholdSummaryReport> ReportSummaries()
        {
            ReportIdle.Wait();
            try
            {
                ReportIdle.Reset();

                // it would be more elegant to use yield return, but that shouldn't be mixed with semaphores
                // in case it is only partially iterated.
                var results = new List<ThresholdSummaryReport>(CoreQueues.Count);
                foreach (var serviceQueue in CoreQueues.Values)
                {
                    if (serviceQueue._sampleCount > 0)
                    {
                        results.Add(serviceQueue.ReportAndReset());
                    }
                }

                return results;
            }
            finally
            {
                ReportIdle.Set();
            }
        }
    }
}
