using System;
using Grpc.Core;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;

public class WorkloadStreamingThread
{
    public static async Task ConsumeResultsAsync(
        IServerStreamWriter<Couchbase.Grpc.Protocol.Run.Result> responseStream,
        Couchbase.Grpc.Protocol.Run.Config runConfig,
        CancellationToken token,
        Channel<Couchbase.Grpc.Protocol.Run.Result> channel, Action incrementConsumed)
    {

        Serilog.Log.Debug("Result streaming started with config {Config}", runConfig);

        try
        {
            if (runConfig?.StreamingConfig is { HasBatchSize: true })
            {
                await ConsumeBatchModeAsync(responseStream, runConfig.StreamingConfig.BatchSize, channel, incrementConsumed, token)
                    .ConfigureAwait(false);
            }
            else
            {
                await ConsumeSingleItemModeAsync(responseStream, channel, incrementConsumed, token)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            Serilog.Log.Information("Streaming operation was cancelled");
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Unexpected error in result streaming");
            throw;
        }
        finally
        {
            Serilog.Log.Debug("Finished results streaming");
        }
    }

    private static async Task ConsumeBatchModeAsync(
        IServerStreamWriter<Couchbase.Grpc.Protocol.Run.Result> responseStream,
        int batchSize,
        Channel<Couchbase.Grpc.Protocol.Run.Result> channel,
        Action incrementConsumed,
        CancellationToken token)
    {
        var batch = new List<Couchbase.Grpc.Protocol.Run.Result>(batchSize);

        while (await channel.Reader.WaitToReadAsync(token).ConfigureAwait(false))
        {
            while (batch.Count < batchSize && channel.Reader.TryRead(out var item))
            {
                batch.Add(item);
                incrementConsumed.Invoke();
            }

            if (batch.Count > 0)
            {
                var batchedResult = new Couchbase.Grpc.Protocol.Run.Result
                {
                    Batched = new Couchbase.Grpc.Protocol.Run.BatchedResult
                    {
                        Result = { batch }
                    }
                };
                await responseStream.WriteAsync(batchedResult, CancellationToken.None).ConfigureAwait(false);
                batch.Clear();
            }
        }
    }

    private static async Task ConsumeSingleItemModeAsync(
        IServerStreamWriter<Couchbase.Grpc.Protocol.Run.Result> responseStream,
        Channel<Couchbase.Grpc.Protocol.Run.Result> channel,
        Action incrementConsumed,
        CancellationToken token)
    {
        while (await channel.Reader.WaitToReadAsync(token).ConfigureAwait(false))
        {
            while (channel.Reader.TryRead(out var item))
            {
                await responseStream.WriteAsync(item, CancellationToken.None).ConfigureAwait(false);
                incrementConsumed.Invoke();
            }
        }
    }
}