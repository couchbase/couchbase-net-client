using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.FitPerformer.Utils;
using Couchbase.Grpc.Protocol.Run;
using Couchbase.Grpc.Protocol.Streams;
using Couchbase.KeyValue;

namespace Couchbase.FitPerformer.Workload.Streams;

public class GetAllReplicasStream : IPerformerStream
{
    public string RunId { get; }
    public string StreamId { get; set; }
    public bool OnDemand { get; set; }
    public int RequestedItemsCount { get; set; }
    public Func<Couchbase.Grpc.Protocol.Run.Result, Task> WriteToChannel { get; set; }
    public Func<Task<IGetReplicaResult>, Task<Result>> ConvertResult { get; set; }
    public Grpc.Protocol.Streams.Config StreamConfig { get; }
    public bool HasFinished { get; set; }
    public CancellationTokenSource Cts { get; set; }

    private readonly IEnumerable<Task<IGetReplicaResult>> _enumerable;

    public GetAllReplicasStream(string runId,
        Couchbase.Grpc.Protocol.Streams.Config streamConfig,
        Func<Couchbase.Grpc.Protocol.Run.Result, Task> writeToChannel,
        Func<Task<IGetReplicaResult>, Task<Result>> convertResult,
        IEnumerable<Task<IGetReplicaResult>> enumerable)
    {
        OnDemand = streamConfig.StreamWhenCase != Grpc.Protocol.Streams.Config.StreamWhenOneofCase.Automatically;
        StreamId = streamConfig.StreamId;
        RunId = runId;
        StreamConfig = streamConfig;
        WriteToChannel = writeToChannel;
        ConvertResult = convertResult;
        _enumerable = enumerable;
        Cts = new CancellationTokenSource();
    }

    public async Task RunAsync()
    {
        HasFinished = false; //Look up Interlocked class

        try
        {
            while (!HasFinished && !Cts.Token.IsCancellationRequested)
            {
                if (!OnDemand)
                {
                    Serilog.Log.Debug("Stream #{StreamId} is returning Automatically", StreamId);

                    foreach (var element in _enumerable)
                    {
                        await EnqueueItemsToGlobalQueue(element).ConfigureAwait(false);
                    }

                    HasFinished = true;
                }

                else
                {
                    using var enumerator = _enumerable.GetEnumerator();
                    Serilog.Log.Debug("Stream #{StreamId} is returning OnDemand", StreamId);

                    while (RequestedItemsCount == 0 && !HasFinished && !Cts.Token.IsCancellationRequested)
                    {
                        await Task.Delay(1).ConfigureAwait(false);
                    }

                    if (HasFinished || Cts.Token.IsCancellationRequested)
                    {
                        break;
                    }

                    int enqueued = 0;
                    bool shouldContinue = true;
                    while (enqueued < RequestedItemsCount && !Cts.Token.IsCancellationRequested && shouldContinue)
                    {
                        shouldContinue = enumerator.MoveNext();
                        if (!shouldContinue)
                        {
                            HasFinished = true;
                            break;
                        }

                        await EnqueueItemsToGlobalQueue(enumerator.Current).ConfigureAwait(false);
                        enqueued++;
                    }
                }
                RequestedItemsCount = 0;
            }

            if (Cts.Token.IsCancellationRequested)
            {
                await SignalStreamCanceledAsync().ConfigureAwait(false);
                HasFinished = true;
            }
            else
            {
                await SignalStreamCompletedAsync().ConfigureAwait(false);
                HasFinished = true;
            }
        }
        catch (Exception e)
        {
            await SignalStreamDiedAsync(e).ConfigureAwait(false);
            HasFinished = true;
            Serilog.Log.Debug("Error in stream #{ID} {Error}", StreamId, e);
        }
    }

    private async Task EnqueueItemsToGlobalQueue(Task<IGetReplicaResult> next)
    {
        var result = await ConvertResult.Invoke(next).ConfigureAwait(false);
        await WriteToChannel.Invoke(result).ConfigureAwait(false);
    }

    private async Task SignalStreamCompletedAsync()
    {
        Serilog.Log.Debug("Stream #{StreamId} Signalling Completion", StreamId);
        var complete = new Couchbase.Grpc.Protocol.Run.Result();
        complete.Stream = new Signal
        {
            Complete = new Complete()
        };
        complete.Stream.Complete.StreamId = StreamId;
        await WriteToChannel.Invoke(complete).ConfigureAwait(false);

    }

    private async Task SignalStreamCanceledAsync()
    {
        Serilog.Log.Debug("Stream #{StreamId} Signalling Canceled", StreamId);
        var canceled = new Couchbase.Grpc.Protocol.Run.Result();
        canceled.Stream = new Signal
        {
            Cancelled = new Cancelled()
        };
        canceled.Stream.Cancelled.StreamId = StreamId;
        await WriteToChannel.Invoke(canceled).ConfigureAwait(false);
    }

    private async Task SignalStreamDiedAsync(Exception e)
    {
        Serilog.Log.Debug("Stream #{StreamId} Signalling Error", StreamId);
        HasFinished = true;
        var error = new Couchbase.Grpc.Protocol.Run.Result();
        error.Stream = new Signal
        {
            Error = new Grpc.Protocol.Streams.Error()
        };
        error.Stream.Error.Exception = ErrorsUtil.ConvertException(e);
        error.Stream.Error.StreamId = StreamId;
        await WriteToChannel.Invoke(error).ConfigureAwait(false);
    }

    public void RequestItems(int amount)
    {
        if (RequestedItemsCount != 0)
        {
            throw new SystemException($"Cannot request more items from Stream #{StreamId} as it has not finished streaming the previously requested items.");
        }
        RequestedItemsCount = amount;
    }

    public async Task CancelAsync()
    {
        if (OnDemand)
        {
            Serilog.Log.Debug("Cancelling stream #{StreamId}", StreamId);
            Cts.Cancel();
            await SignalStreamCanceledAsync().ConfigureAwait(false);
        }
        else
        {
            throw new SystemException("Cannot cancel an Automatic stream.");
        }
    }
}