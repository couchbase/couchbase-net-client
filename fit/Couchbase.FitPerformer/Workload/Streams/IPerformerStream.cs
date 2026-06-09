using System;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.FitPerformer.Workload.Streams;

public interface IPerformerStream
{
    public string RunId { get; }
    public string StreamId { get; set; }
    public bool OnDemand { get; set; }
    public int RequestedItemsCount { get; set; }
    public Func<Couchbase.Grpc.Protocol.Run.Result, Task> WriteToChannel { get; set; }
    public Couchbase.Grpc.Protocol.Streams.Config StreamConfig { get; }
    public Task RunAsync();
    public void RequestItems(int amount);
    public Task CancelAsync();
    public bool HasFinished { get; set; }
    public CancellationTokenSource Cts { get; set; }
}