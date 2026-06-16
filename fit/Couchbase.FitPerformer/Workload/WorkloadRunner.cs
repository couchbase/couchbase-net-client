using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.FitPerformer.Workload;
using Couchbase.FitPerformer.Workload.Streams;

namespace Couchbase.FitPerformer
{

    static class WorkloadRunExecutor
    {
        public static async Task RunWorkloadsAsync(Couchbase.Grpc.Protocol.Run.Workloads workloads, Func<Couchbase.Grpc.Protocol.Run.Result, Task> writeToChannel, Counters counters, Utils.ClusterConnection connection, string runId, StreamOwner streamOwner, ConcurrentDictionary<string, IRequestSpan> spans)
        {
            var runners = new List<Task>();

            foreach (var runnerTask in workloads.HorizontalScaling)
            {
                runners.Add(HorizontalScalingThread.PerformCommandsAsync(runnerTask, writeToChannel, counters, connection, runId, streamOwner, spans));
            }

            Serilog.Log.Debug("Waiting for {Count} runner(s) to finish", runners.Count);

            await Task.WhenAll(runners).ConfigureAwait(false);

            Serilog.Log.Debug("Waiting for {Count} stream(s) to finish", streamOwner.GetStreamsCount());

            await streamOwner.WaitForStreamsToFinishAsync(runId).ConfigureAwait(false);

            Serilog.Log.Debug("All {Count} runners finished", runners.Count);
        }
    }
}