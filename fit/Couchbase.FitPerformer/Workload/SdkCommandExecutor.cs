using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Grpc.Protocol.Shared;
using Couchbase.KeyValue;
using System.Threading;
using System.Diagnostics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.FitPerformer.Utils;
using Couchbase.FitPerformer.Utils.Options;
using Couchbase.FitPerformer.Utils.Results;
using Couchbase.FitPerformer.Workload;
using Couchbase.FitPerformer.Workload.Streams;
using Google.Protobuf.WellKnownTypes;
using Couchbase.Grpc.Protocol.Streams;
using Command = Couchbase.Grpc.Protocol.Sdk.Command;
using Type = Couchbase.Grpc.Protocol.Streams.Type;

namespace Couchbase.FitPerformer
{

    internal class SdkCommandExecutor
    {
        private static readonly ThreadLocal<Random> Random = new ThreadLocal<Random>(() => new Random(Environment.TickCount));
        private static StreamOwner _streamOwner;
        private static Func<Couchbase.Grpc.Protocol.Run.Result, Task> _writeToChannel;
        private static string _runId;
        private static ConcurrentDictionary<string, IRequestSpan> _spans;
        private static Counters _counters;

        public static async Task<Couchbase.Grpc.Protocol.Run.Result> RunCommand(Couchbase.Grpc.Protocol.Sdk.Command command, Counters counters, ClusterConnection connection, Func<Couchbase.Grpc.Protocol.Run.Result, Task> writeToChannel, string runId, StreamOwner streamOwner, ConcurrentDictionary<string, IRequestSpan> spans)
        {
            _writeToChannel = writeToChannel;
            _streamOwner = streamOwner;
            _runId = runId;
            _spans = spans;
            _counters = counters;

            // These two times are only used for error results.  They won't be quite as accurate as requested by the RPC, but.. it's only for error results.
            var initiated = GetTimeNow();
            var sw = Stopwatch.StartNew();

            Couchbase.Grpc.Protocol.Run.Result ret = new Grpc.Protocol.Run.Result();
            try
            {
                ret = await ExecuteCommand(command, connection).ConfigureAwait(false);
            }
            catch (System.Exception err)
            {
                //var ret = new Couchbase.Grpc.Protocol.Run.Result();
                ret.Sdk = new Couchbase.Grpc.Protocol.Sdk.Result();
                ret.Initiated = initiated;
                sw.Stop();
                ret.ElapsedNanos = sw.Elapsed.CalculateNanos();
                ret.Sdk.Exception = Couchbase.FitPerformer.Utils.ErrorsUtil.ConvertException(err);
                Serilog.Log.Debug(err.ToString());
            }

            return ret;
        }

        private static Timestamp GetTimeNow()
        {
            return Timestamp.FromDateTime(DateTime.UtcNow);
        }

        private static async Task<Couchbase.Grpc.Protocol.Run.Result> ExecuteCommand(Couchbase.Grpc.Protocol.Sdk.Command op, ClusterConnection connection)
        {
            var result = new Couchbase.Grpc.Protocol.Run.Result();
            // Serilog.Log.Debug("Executing {Op}", op);

            switch (op.CommandCase)
            {
                case Command.CommandOneofCase.Insert:
                    {
                        var request = op.Insert;
                        var collection = await connection.GetCollectionAsync(request.Location).ConfigureAwait(false);
                        var content = ResultsUtil.Content(request.Content);
                        var docId = GetDocId(request.Location, _counters);
                        var options = OptionsUtil.CreateOptions(request, _spans);
                        result.Initiated = GetTimeNow();
                        var sw = Stopwatch.StartNew();
                        IMutationResult mr;
                        //If the content is null, wrap it in a dynamic type to bypass type-checking here.
                        //If not, let the .NET Runtime infer the type without re-wrapping it in a dynamic type.
                        if (content is null)
                        {
                            mr = options is null
                                ? await collection.InsertAsync<dynamic>(docId, content).ConfigureAwait(false)
                                : await collection.InsertAsync<dynamic>(docId, content, options).ConfigureAwait(false);
                        }
                        else
                        {
                            mr = options is null
                                ? await collection.InsertAsync(docId, content).ConfigureAwait(false)
                                : await collection.InsertAsync(docId, content, options).ConfigureAwait(false);
                        }

                        sw.Stop();
                        result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                        if (op.ReturnResult) ResultsUtil.PopulateResult(result, mr);
                        else ResultsUtil.SetSuccess(result);
                        break;
                    }

                case Command.CommandOneofCase.Get:
                    {
                        var request = op.Get;
                        var collection = await connection.GetCollectionAsync(request.Location).ConfigureAwait(false);
                        var docId = GetDocId(request.Location, _counters);
                        var options = OptionsUtil.CreateOptions(request, _spans);
                        result.Initiated = GetTimeNow();
                        var sw = Stopwatch.StartNew();
                        IGetResult gr;
                        if (options == null) gr = await collection.GetAsync(docId).ConfigureAwait(false);
                        else gr = await collection.GetAsync(docId, options).ConfigureAwait(false);
                        sw.Stop();
                        result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                        if (op.ReturnResult) ResultsUtil.PopulateResult(result, gr, request.ContentAs.AsCase);
                        else ResultsUtil.SetSuccess(result);
                        break;
                    }

                case Command.CommandOneofCase.Remove:
                    {
                        var request = op.Remove;
                        var collection = await connection.GetCollectionAsync(request.Location).ConfigureAwait(false);
                        var docId = GetDocId(request.Location, _counters);
                        var options = OptionsUtil.CreateOptions(request, _spans);
                        result.Initiated = GetTimeNow();
                        var sw = Stopwatch.StartNew();
                        if (options == null) await collection.RemoveAsync(docId).ConfigureAwait(false);
                        else await collection.RemoveAsync(docId, options).ConfigureAwait(false);
                        sw.Stop();
                        result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                        // if (op.ReturnResult) .NET SDK does not return an IMutationResult for Collection.Remove()
                        ResultsUtil.SetSuccess(result);
                        break;
                    }

                case Command.CommandOneofCase.Replace:
                    {
                        var request = op.Replace;
                        var collection = await connection.GetCollectionAsync(request.Location).ConfigureAwait(false);
                        var docId = GetDocId(request.Location, _counters);
                        var options = OptionsUtil.CreateOptions(request, _spans);
                        var content = ResultsUtil.Content(request.Content);
                        result.Initiated = GetTimeNow();
                        var sw = Stopwatch.StartNew();
                        IMutationResult mr;
                        if (content is null)
                        {
                            mr = options is null
                                ? await collection.ReplaceAsync<dynamic>(docId, content).ConfigureAwait(false)
                                : await collection.ReplaceAsync<dynamic>(docId, content, options).ConfigureAwait(false);
                        }
                        else
                        {
                            mr = options is null
                                ? await collection.ReplaceAsync(docId, content).ConfigureAwait(false)
                                : await collection.ReplaceAsync(docId, content, options).ConfigureAwait(false);
                        }

                        sw.Stop();
                        result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                        if (op.ReturnResult) ResultsUtil.PopulateResult(result, mr);
                        else ResultsUtil.SetSuccess(result);
                        break;
                    }

                case Command.CommandOneofCase.Upsert:
                    {
                        var request = op.Upsert;
                        var collection = await connection.GetCollectionAsync(request.Location).ConfigureAwait(false);
                        var docId = GetDocId(request.Location, _counters);
                        var options = OptionsUtil.CreateOptions(request, _spans);
                        var content = ResultsUtil.Content(request.Content);
                        result.Initiated = GetTimeNow();
                        var sw = Stopwatch.StartNew();
                        IMutationResult mr;
                        if (content is null)
                        {
                            mr = options is null
                                ? await collection.UpsertAsync<dynamic>(docId, content).ConfigureAwait(false)
                                : await collection.UpsertAsync<dynamic>(docId, content, options).ConfigureAwait(false);
                        }
                        else
                        {
                            mr = options is null
                                ? await collection.UpsertAsync(docId, content).ConfigureAwait(false)
                                : await collection.UpsertAsync(docId, content, options).ConfigureAwait(false);
                        }
                        sw.Stop();
                        result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                        if (op.ReturnResult) ResultsUtil.PopulateResult(result, mr);
                        else ResultsUtil.SetSuccess(result);
                        break;
                    }
                case Command.CommandOneofCase.ClusterCommand:
                {
                    await ClusterBucketScopeCollectionCommandExecutor.RunCommand(op, connection, result, _spans, _runId, _writeToChannel, _streamOwner, _counters).ConfigureAwait(false);
                    break;
                }
                case Command.CommandOneofCase.BucketCommand:
                {
                    await ClusterBucketScopeCollectionCommandExecutor.RunCommand(op, connection, result, _spans, _runId, _writeToChannel, _streamOwner, _counters).ConfigureAwait(false);
                    break;
                }
                case Command.CommandOneofCase.ScopeCommand:
                {
                    await ClusterBucketScopeCollectionCommandExecutor.RunCommand(op, connection, result, _spans, _runId, _writeToChannel, _streamOwner, _counters).ConfigureAwait(false);
                    break;
                }
                case Command.CommandOneofCase.CollectionCommand:
                {
                    await ClusterBucketScopeCollectionCommandExecutor.RunCommand(op, connection, result, _spans, _runId, _writeToChannel, _streamOwner, _counters).ConfigureAwait(false);
                    break;
                }
                case Command.CommandOneofCase.RangeScan:
                {
                    var request = op.RangeScan;
                    var bucket = await connection.GetBucketAsync(request.Collection.BucketName).ConfigureAwait(false);
                    var collection = await bucket.CollectionAsync(request.Collection.CollectionName).ConfigureAwait(false);
                    var options = OptionsUtil.CreateOptions(request);
                    var scanType = OptionsUtil.ConvertScanType(request);
                    result.Initiated = GetTimeNow();
                    var sw = Stopwatch.StartNew();
                    IAsyncEnumerable<Couchbase.KeyValue.RangeScan.IScanResult> scan;
                    if (options != null) scan = collection.ScanAsync(scanType, options);
                    else scan = collection.ScanAsync(scanType);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();

                    var stream = new AsyncStream<Couchbase.KeyValue.RangeScan.IScanResult>(_runId, op.RangeScan.StreamConfig, _writeToChannel, scanResult => ResultsUtil.ProcessScanResult(scanResult, op.RangeScan.StreamConfig.StreamId, request.ContentAs), scan);

                    _streamOwner.InitializeNewStream(stream);

                    result.Stream = new Signal
                    {
                        Created = new Created()
                    };
                    result.Stream.Created.StreamId = stream.StreamId;
                    result.Stream.Created.Type = Type.StreamKvRangeScan;

                    break;
                }
                case Command.CommandOneofCase.None:
                    goto case default;
                default:
                    throw new NotSupportedException();
            }

            // Serilog.Log.Debug("Op took {V}ns", result.ElapsedNanos);
            //Serilog.Log.Debug("Returning a {R}", result.Sdk.ResultCase ?? "Something");
            return result;
        }

        private static String GetDocId(DocLocation location, Counters counters)
        {
            if (location.LocationCase == DocLocation.LocationOneofCase.Specific)
            {
                return location.Specific.Id;
            }
            else if (location.LocationCase == DocLocation.LocationOneofCase.Uuid)
            {
                return Guid.NewGuid().ToString();
            }
            else if (location.LocationCase == DocLocation.LocationOneofCase.Pool)
            {
                var pool = location.Pool;

                int next;
                if (pool.PoolSelectionStrategyCase == DocLocationPool.PoolSelectionStrategyOneofCase.Random)
                {
                    if (pool.Random.Distribution == RandomDistribution.Uniform) {
                        next = Random.Value.Next((int)pool.PoolSize);
                    }
                    else {
                        throw new NotSupportedException();
                    }
                }
                else if (pool.PoolSelectionStrategyCase == DocLocationPool.PoolSelectionStrategyOneofCase.Counter)
                {
                    var counter = counters.GetCounter(pool.Counter.Counter);
                    next = counter.IncrementAndGet() % (int)pool.PoolSize;
                }
                else
                {
                    throw new NotSupportedException("Unrecognised pool selection strategy");
                }

                return pool.IdPreface + next;
            }
            else
            {
                throw new NotSupportedException("Unknown doc location type");
            }
        }
    }
}