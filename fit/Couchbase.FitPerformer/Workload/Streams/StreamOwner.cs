using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Couchbase.FitPerformer.Workload.Streams;

public class StreamOwner
{
    private ConcurrentDictionary<IPerformerStream, Task> _streams = new ConcurrentDictionary<IPerformerStream, Task>();

    public void InitializeNewStream(IPerformerStream performerStream)
    {
        var runner = Task.Run(async () =>
        {
            await performerStream.RunAsync().ConfigureAwait(false);
        });
        _streams.TryAdd(performerStream, runner);
    }

    public async Task WaitForStreamsToFinishAsync(string runId)
    {
        var streamsInRun = _streams.Where(x => x.Key.RunId.Equals(runId)).ToList();
        var tasksInRun = streamsInRun.Select(x => x.Value).ToList();

        await Task.WhenAll(tasksInRun);

        foreach (var entry in streamsInRun)
        {
            _streams.TryRemove(entry);
        }

        Serilog.Log.Debug("StreamOwner: All Streams have finished returning");
    }

    public void RequestItems(Grpc.Protocol.Streams.RequestItemsRequest requestItemsRequest)
    {
        var stream = GetStream(requestItemsRequest.StreamId);
        stream.RequestItems(requestItemsRequest.NumItems);
    }

    public IPerformerStream GetStream(string streamId)
    {
        var stream = _streams.Keys.ToList().Find(x => x.StreamId == streamId);
        if (stream == null) throw new ArgumentNullException(nameof(stream), "Could not find stream.");

        return stream;
    }

    public void Cancel(Grpc.Protocol.Streams.CancelRequest cancelRequest)
    {
        var stream = GetStream(cancelRequest.StreamId);
        stream.CancelAsync();
    }

    public int GetStreamsCount()
    {
        return _streams.Count;
    }
}