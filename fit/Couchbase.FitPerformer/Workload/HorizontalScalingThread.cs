using Couchbase.Grpc.Protocol.Shared;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.FitPerformer.Workload;
using Couchbase.FitPerformer.Workload.Streams;

namespace Couchbase.FitPerformer
{
    class HorizontalScalingThread
    {
        public static async Task PerformCommandsAsync(Grpc.Protocol.Run.HorizontalScaling perThread, Func<Couchbase.Grpc.Protocol.Run.Result, Task> writeToChannel, Counters counters, Utils.ClusterConnection connection, string runId, StreamOwner streamOwner, ConcurrentDictionary<string, IRequestSpan> spans)
        {
            foreach (var workload in perThread.Workloads)
            {
                if (workload.Sdk == null) throw new NotSupportedException("Non-SDK Workloads are not supported.");

                var bounds = GetBounds(workload.Sdk.Bounds, counters, workload.Sdk.Command.Count);

                for (var executed = 0; bounds.CanExecute(); executed++)
                {
                    var next = workload.Sdk.Command[executed % workload.Sdk.Command.Count];

                    var result = await SdkCommandExecutor.RunCommand(next, counters, connection, writeToChannel, runId, streamOwner, spans).ConfigureAwait(false);
                    await writeToChannel.Invoke(result);
                }
            }
        }


        private static BoundsExecutor GetBounds(Bounds bounds, Counters counters, int nCommands)
        {
            if (bounds == null || bounds.BoundsCase == Bounds.BoundsOneofCase.None)
            {
                return new BoundsCounterBased(new Counter(nCommands));
            }
            if (bounds.BoundsCase == Bounds.BoundsOneofCase.Counter)
            {
                var counter = counters.GetCounter(bounds.Counter);
                return new BoundsCounterBased(counter);
            }
            if (bounds.BoundsCase == Bounds.BoundsOneofCase.ForTime)
            {
                return new BoundsForTime(bounds.ForTime.Seconds);
            }

            throw new NotSupportedException();

        }
    }
}