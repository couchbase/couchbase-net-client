using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Authentication.Authenticators;
using Couchbase.FitPerformer.Utils;
using Couchbase.FitPerformer.Utils.Options;
using Couchbase.FitPerformer.Utils.Results;
using Couchbase.FitPerformer.Workload;
using Couchbase.FitPerformer.Workload.Streams;
using Couchbase.Grpc.Protocol.Sdk;
using Couchbase.Grpc.Protocol.Shared;
using Couchbase.Grpc.Protocol.Streams;
using Couchbase.KeyValue;
using Google.Protobuf.WellKnownTypes;
using Newtonsoft.Json;
using Serilog;
using Command = Couchbase.Grpc.Protocol.Sdk.Query.IndexManager.Command;
using Result = Couchbase.Grpc.Protocol.Run.Result;
using SearchIndex = Couchbase.Management.Search.SearchIndex;

#nullable enable
namespace Couchbase.FitPerformer
{
    internal static class ClusterBucketScopeCollectionCommandExecutor
    {
        private static ConcurrentDictionary<string, IRequestSpan> _spans;
        private static string _runId;
        private static Func<Couchbase.Grpc.Protocol.Run.Result, Task> _writeToChannel;
        private static StreamOwner _streamOwner;
        private static Counters _counters;

        internal static async Task RunCommand(Couchbase.Grpc.Protocol.Sdk.Command op,
            ClusterConnection connection, Grpc.Protocol.Run.Result result,
            ConcurrentDictionary<string, IRequestSpan> spans, string runId,
            Func<Couchbase.Grpc.Protocol.Run.Result, Task> writeToChannel, StreamOwner streamOwner,
            Counters counters)
        {
            _spans = spans;
            _runId = runId;
            _writeToChannel = writeToChannel;
            _streamOwner = streamOwner;
            _counters = counters;

            switch (op.CommandCase) //Cluster, Scope, or Collection Command
            {
                case Grpc.Protocol.Sdk.Command.CommandOneofCase.ClusterCommand:
                {
                    await HandleClusterLevelCommand(op, connection, result);
                    break;
                }
                case Grpc.Protocol.Sdk.Command.CommandOneofCase.BucketCommand:
                {
                    await HandleBucketLevelCommand(op, connection, result);
                    break;
                }
                case Grpc.Protocol.Sdk.Command.CommandOneofCase.ScopeCommand:
                {
                    await HandleScopeLevelCommand(op, connection, result);
                    break;
                }
                case Grpc.Protocol.Sdk.Command.CommandOneofCase.CollectionCommand:
                {
                    await HandleCollectionLevelCommand(op, connection, result);
                    break;
                }
            }
        }

        private static async Task HandleCollectionLevelCommand(Grpc.Protocol.Sdk.Command op, ClusterConnection connection, Result result)
        {
            ICouchbaseCollection collection = null;
            var opCollection = op.CollectionCommand.Collection;
            if (opCollection != null)
            {
                collection = await connection.GetCollectionAsync(opCollection.BucketName, opCollection.ScopeName, opCollection.CollectionName).ConfigureAwait(false);
            }

            switch (op.CollectionCommand.CommandCase)
            {
                case CollectionLevelCommand.CommandOneofCase.Touch:
                {
                    var request = op.CollectionCommand.Touch;
                    var docId = CommandUtils.GetDocId(request.Location, _counters);
                    var expiry = CommandUtils.ConvertExpiry(request.Expiry);
                    var options = OptionsUtil.CreateOptions(request, _spans);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    IMutationResult touchResult = options is null
                        ? await collection.TouchWithCasAsync(docId, expiry).ConfigureAwait(false)
                        : await collection.TouchWithCasAsync(docId, expiry, options).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    if (op.ReturnResult) ResultsUtil.PopulateResult(result, touchResult);
                    break;
                }
                case CollectionLevelCommand.CommandOneofCase.GetAndTouch:
                {
                    var request = op.CollectionCommand.GetAndTouch;
                    var docId = CommandUtils.GetDocId(request.Location, _counters);
                    var expiry = CommandUtils.ConvertExpiry(request.Expiry);
                    var options = OptionsUtil.CreateOptions(request, _spans);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    IGetResult gr;
                    if (options == null) gr = await collection.GetAndTouchAsync(docId, expiry).ConfigureAwait(false);
                    else gr = await collection.GetAndTouchAsync(docId, expiry, options).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    if (op.ReturnResult) ResultsUtil.PopulateResult(result, gr, request.ContentAs.AsCase);
                    else ResultsUtil.SetSuccess(result);
                    break;
                }
                case CollectionLevelCommand.CommandOneofCase.GetAndLock:
                {
                    var request = op.CollectionCommand.GetAndLock;
                    var docId = CommandUtils.GetDocId(request.Location, _counters);
                    var duration = TimeSpan.FromSeconds(request.Duration.Seconds);
                    var options = OptionsUtil.CreateOptions(request, _spans);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    IGetResult gr;
                    if (options == null)
                        gr = await collection.GetAndLockAsync(docId, duration).ConfigureAwait(false);
                    else gr = await collection.GetAndLockAsync(docId, duration, options).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    if (op.ReturnResult) ResultsUtil.PopulateResult(result, gr, request.ContentAs.AsCase);
                    else ResultsUtil.SetSuccess(result);
                    break;
                }
                case CollectionLevelCommand.CommandOneofCase.Unlock:
                {
                    var request = op.CollectionCommand.Unlock;
                    var docId = CommandUtils.GetDocId(request.Location, _counters);
                    var cas = request.Cas;
                    var options = OptionsUtil.CreateOptions(request, _spans);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    if (options == null) await collection.UnlockAsync(docId, (ulong)cas).ConfigureAwait(false);
                    else await collection.UnlockAsync(docId, (ulong)cas, options).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    ResultsUtil.SetSuccess(result);
                    break;
                }
                case CollectionLevelCommand.CommandOneofCase.Exists:
                {
                    var request = op.CollectionCommand.Exists;
                    var docId = CommandUtils.GetDocId(request.Location, _counters);
                    var options = OptionsUtil.CreateOptions(request, _spans);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    IExistsResult exists;
                    if (options == null) exists = await collection.ExistsAsync(docId).ConfigureAwait(false);
                    else exists = await collection.ExistsAsync(docId, options).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    if (op.ReturnResult) ResultsUtil.PopulateResult(result, exists);
                    else ResultsUtil.SetSuccess(result);
                    break;
                }
                case CollectionLevelCommand.CommandOneofCase.MutateIn:
                {
                    var request = op.CollectionCommand.MutateIn;
                    var docId = CommandUtils.GetDocId(request.Location, _counters);
                    var options = OptionsUtil.CreateOptions(request, _spans);
                    var specs = request.Spec.Select(CommandUtils.ConvertMutateInSpec);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    IMutateInResult gr;
                    if (options == null)
                        gr = await collection.MutateInAsync(docId, specs).ConfigureAwait(false);
                    else gr = await collection.MutateInAsync(docId, specs, options).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    if (op.ReturnResult) ResultsUtil.PopulateResult(result, gr, request);
                    else ResultsUtil.SetSuccess(result);
                    break;
                }
                case CollectionLevelCommand.CommandOneofCase.GetAllReplicas:
                {
                    var request = op.CollectionCommand.GetAllReplicas;
                    var docId = CommandUtils.GetDocId(request.Location, _counters);
                    var options = OptionsUtil.CreateOptions(request, _spans);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();

                    IEnumerable<Task<IGetReplicaResult>> gr;
                    if (options == null) gr = collection.GetAllReplicasAsync(docId);
                    else gr = collection.GetAllReplicasAsync(docId, options);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    var stream = new GetAllReplicasStream(_runId, request.StreamConfig,
                        _writeToChannel,
                        getReplicaResultTask => ResultsUtil.ProcessGetReplicasResult(getReplicaResultTask, request),
                        gr);
                    _streamOwner.InitializeNewStream(stream);
                    result.Stream = new Signal
                    {
                        Created = new Created()
                    };
                    result.Stream.Created.StreamId = stream.StreamId;
                    result.Stream.Created.Type = Grpc.Protocol.Streams.Type.StreamKvGetAllReplicas;
                    break;
                }
                case CollectionLevelCommand.CommandOneofCase.GetAnyReplica:
                {
                    var request = op.CollectionCommand.GetAnyReplica;
                    var docId = CommandUtils.GetDocId(request.Location, _counters);
                    var options = OptionsUtil.CreateOptions(request, _spans);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    IGetReplicaResult gr;
                    if (options == null) gr = await collection.GetAnyReplicaAsync(docId).ConfigureAwait(false);
                    else gr = await collection.GetAnyReplicaAsync(docId, options).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    if (op.ReturnResult) ResultsUtil.PopulateResult(result, gr, request.ContentAs.AsCase);
                    else ResultsUtil.SetSuccess(result);
                    break;
                }
                case CollectionLevelCommand.CommandOneofCase.Binary:
                {
                    var blc = op.CollectionCommand.Binary;
                    switch (blc.CommandCase)
                    {
                        case BinaryCollectionLevelCommand.CommandOneofCase.Append:
                        {
                            var request = blc.Append;
                            var docId = CommandUtils.GetDocId(request.Location, _counters);
                            var options = OptionsUtil.CreateOptions(request, _spans);
                            var values = request.Content.ToByteArray();
                            result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                            var sw = Stopwatch.StartNew();
                            IMutationResult gr;
                            if (options == null) gr = await collection.Binary.AppendAsync(docId, values).ConfigureAwait(false);
                            else gr = await collection.Binary.AppendAsync(docId, values, options).ConfigureAwait(false);
                            sw.Stop();
                            result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                            if (op.ReturnResult) ResultsUtil.PopulateResult(result, gr);
                            else ResultsUtil.SetSuccess(result);
                            break;
                        }
                        case BinaryCollectionLevelCommand.CommandOneofCase.Prepend:
                        {
                            var request = blc.Prepend;
                            var docId = CommandUtils.GetDocId(request.Location, _counters);
                            var options = OptionsUtil.CreateOptions(request, _spans);
                            var values = request.Content.ToByteArray();
                            result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                            var sw = Stopwatch.StartNew();
                            IMutationResult gr;
                            if (options == null) gr = await collection.Binary.PrependAsync(docId, values).ConfigureAwait(false);
                            else gr = await collection.Binary.PrependAsync(docId, values, options).ConfigureAwait(false);
                            sw.Stop();
                            result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                            if (op.ReturnResult) ResultsUtil.PopulateResult(result, gr);
                            else ResultsUtil.SetSuccess(result);
                            break;
                        }
                        case BinaryCollectionLevelCommand.CommandOneofCase.Increment:
                        {
                            var request = blc.Increment;
                            var docId = CommandUtils.GetDocId(request.Location, _counters);
                            var options = OptionsUtil.CreateOptions(request, _spans);
                            result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                            var sw = Stopwatch.StartNew();
                            ICounterResult gr;
                            if (options == null) gr = await collection.Binary.IncrementAsync(docId).ConfigureAwait(false);
                            else gr = await collection.Binary.IncrementAsync(docId, options).ConfigureAwait(false);
                            sw.Stop();
                            result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                            if (op.ReturnResult) ResultsUtil.PopulateResult(result, gr);
                            else ResultsUtil.SetSuccess(result);
                            break;
                        }
                        case BinaryCollectionLevelCommand.CommandOneofCase.Decrement:
                        {
                            var request = blc.Decrement;
                            var docId = CommandUtils.GetDocId(request.Location, _counters);
                            var options = OptionsUtil.CreateOptions(request, _spans);
                            result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                            var sw = Stopwatch.StartNew();
                            ICounterResult gr;
                            if (options == null) gr = await collection.Binary.DecrementAsync(docId).ConfigureAwait(false);
                            else gr = await collection.Binary.DecrementAsync(docId, options).ConfigureAwait(false);
                            sw.Stop();
                            result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                            if (op.ReturnResult) ResultsUtil.PopulateResult(result, gr);
                            else ResultsUtil.SetSuccess(result);
                            break;
                        }
                    }
                    break;
                }
                case CollectionLevelCommand.CommandOneofCase.QueryIndexManager:
                {
                    switch (op.CollectionCommand.QueryIndexManager.CommandCase)
                    {
                        case Grpc.Protocol.Sdk.Collection.Query.IndexManager.Command.CommandOneofCase.Shared:
                        {
                            await RunSharedIndexManagementCommand(op.CollectionCommand, collection, result).ConfigureAwait(false);
                            break;
                        }
                        default:
                            throw new NotImplementedException("Unknown Collection Query Index Command");
                    }
                    break;
                }
                case CollectionLevelCommand.CommandOneofCase.LookupIn:
                {
                    var request = op.CollectionCommand.LookupIn;
                    var options = OptionsUtil.ConvertLookupInOptions(request.Options);
                    var rawByteTranscoder = RawByteArrayTranscoderIfNeeded(
                        request.Spec.Any(s => s.ContentAs.AsCase == ContentAs.AsOneofCase.AsByteArray), connection);
                    if (rawByteTranscoder != null) options = options.Transcoder(rawByteTranscoder);
                    var specs = request.Spec.Select(ResultsUtil.ConvertLookupInSpec);
                    var (coll, id) = await CommandUtils.DetermineLocation(request.Location, connection, _counters).ConfigureAwait(false);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    var lookupResult = await coll.LookupInAsync(id, specs, options).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    ResultsUtil.ProcessLookupInResult(lookupResult, result, request.Spec);
                    break;
                }
                case CollectionLevelCommand.CommandOneofCase.LookupInAllReplicas:
                {
                    var request = op.CollectionCommand.LookupInAllReplicas;
                    var options = OptionsUtil.ConvertLookupInAllReplicasOptions(request.Options, _spans);
                    var rawByteTranscoder = RawByteArrayTranscoderIfNeeded(
                        request.Spec.Any(s => s.ContentAs.AsCase == ContentAs.AsOneofCase.AsByteArray), connection);
                    if (rawByteTranscoder != null) options = options.Transcoder(rawByteTranscoder);
                    var specs = request.Spec.Select(ResultsUtil.ConvertLookupInSpec);
                    var (coll, id) = await CommandUtils.DetermineLocation(request.Location, connection, _counters).ConfigureAwait(false);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    var lookupResult = coll.LookupInAllReplicasAsync(id, specs, options);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();

                    var stream = new AsyncStream<ILookupInReplicaResult>(_runId, request.StreamConfig,
                        _writeToChannel,
                        lookupInResult => ResultsUtil.ProcessLookupInAllReplicasResult(lookupInResult, request.Spec, request.StreamConfig.StreamId),
                        lookupResult);
                    _streamOwner.InitializeNewStream(stream);
                    result.Stream = new Signal
                    {
                        Created = new Created()
                    };
                    result.Stream.Created.StreamId = stream.StreamId;
                    result.Stream.Created.Type = Couchbase.Grpc.Protocol.Streams.Type.StreamLookupInAllReplicas;
                    break;
                }
                case CollectionLevelCommand.CommandOneofCase.LookupInAnyReplica:
                {
                    var request = op.CollectionCommand.LookupInAnyReplica;
                    var options = OptionsUtil.ConvertLookupInAnyReplicasOptions(request.Options, _spans);
                    var rawByteTranscoder = RawByteArrayTranscoderIfNeeded(
                        request.Spec.Any(s => s.ContentAs.AsCase == ContentAs.AsOneofCase.AsByteArray), connection);
                    if (rawByteTranscoder != null) options = options.Transcoder(rawByteTranscoder);
                    var specs = request.Spec.Select(ResultsUtil.ConvertLookupInSpec);
                    var (coll, id) = await CommandUtils.DetermineLocation(request.Location, connection, _counters).ConfigureAwait(false);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    var lookupResult = await coll.LookupInAnyReplicaAsync(id, specs, options).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    ResultsUtil.ProcessLookupInAnyReplicaResult(lookupResult, result, request.Spec);
                    break;
                }
                default:
                    throw new NotSupportedException("Unknown Collection Command");
            }
        }

        // contentAs(byte[]) is a raw-passthrough read cross-SDK (see the Java performer /
        // DefaultJsonSerializer). .NET's default serializer base64s byte[], so a bare
        // contentAs<byte[]> base64-decodes instead of returning the raw wire bytes. When any spec
        // reads byte[], supply a passthrough serializer (mirroring Java) via a per-op transcoder so
        // the raw fragment bytes are returned. Opt-in per-op; does not change the SDK default
        // (Queue<byte[]>/CBSE-22994 unaffected). Decorate the cluster's *configured* serializer
        // (honoring UseCustomSerializer) rather than hard-coding DefaultSerializer, so non-byte[]
        // specs in a mixed-spec lookup keep the cluster's serialization semantics. Shared by the
        // LookupIn / LookupInAllReplicas / LookupInAnyReplica paths.
        private static Couchbase.Core.IO.Transcoders.ITypeTranscoder? RawByteArrayTranscoderIfNeeded(
            bool anyByteArraySpec, ClusterConnection connection)
        {
            if (!anyByteArraySpec) return null;
            var configuredSerializer =
                connection.Cluster.ClusterServices.GetService(typeof(Couchbase.Core.IO.Serializers.ITypeSerializer))
                    as Couchbase.Core.IO.Serializers.ITypeSerializer
                ?? new Couchbase.Core.IO.Serializers.DefaultSerializer();
            return new Couchbase.Core.IO.Transcoders.JsonTranscoder(
                new Couchbase.Core.IO.Serializers.RawByteArraySerializer(configuredSerializer));
        }

        private static async Task HandleScopeLevelCommand(Grpc.Protocol.Sdk.Command op, ClusterConnection connection, Result result)
        {
            var opScope = op.ScopeCommand.Scope;
            var bucket = await connection.GetBucketAsync(opScope.BucketName).ConfigureAwait(false);
            var scope = await bucket.ScopeAsync(opScope.ScopeName).ConfigureAwait(false);

            switch (op.ScopeCommand.CommandCase)
            {
                case ScopeLevelCommand.CommandOneofCase.Query:
                {
                    await QueryHelper.PerformScopeQuery(op.ScopeCommand.Query, _spans, result, scope, op.ReturnResult).ConfigureAwait(false);
                    break;
                }
                case ScopeLevelCommand.CommandOneofCase.Search:
                    throw new NotSupportedException("Scope-level search is only available via SearchV2");
                case ScopeLevelCommand.CommandOneofCase.SearchV2:
                    await SearchHelper.ExecuteSearchV2Query(op.ScopeCommand.SearchV2, _spans, result, scope.Bucket.Cluster, scope)
                        .ConfigureAwait(false);
                    break;
                case ScopeLevelCommand.CommandOneofCase.SearchIndexManager:
                {
                    var opSearchIndex = op.ScopeCommand.SearchIndexManager;
                    var indexManager = scope.SearchIndexes;
                    switch (opSearchIndex.CommandCase)
                    {
                        case Grpc.Protocol.Sdk.Scope.Search.IndexManager.Command.CommandOneofCase.Shared:
                        {
                            await SearchIndexManagementHelper.RunSharedSearchIndexManagementCommand(opSearchIndex.Shared, indexManager, result, scope);
                            break;
                        }
                        case Grpc.Protocol.Sdk.Scope.Search.IndexManager.Command.CommandOneofCase.None:
                        {
                            throw new NotSupportedException();
                        }
                    }
                }
                    break;
                default:
                    throw new NotSupportedException("Unknown Scope Command.");
            }
        }

        private static async Task HandleBucketLevelCommand(Grpc.Protocol.Sdk.Command op, ClusterConnection connection, Result result)
        {
            var opBucket = op.BucketCommand;
            switch (opBucket.CommandCase)
            {
                case BucketLevelCommand.CommandOneofCase.CollectionManager:
                {
                    var bucket = await connection.GetBucketAsync(opBucket.BucketName).ConfigureAwait(false);
                    var collectionMgr = bucket.Collections;
                    var opCollection = opBucket.CollectionManager;
                    switch (opCollection.CommandCase)
                    {
                        case Grpc.Protocol.Sdk.Bucket.CollectionManager.Command.CommandOneofCase.CreateCollection:
                        {
                            var request = opCollection.CreateCollection;
                            var settings = OptionsUtil.ConvertSettings(request.Settings);
                            var options = OptionsUtil.ConvertOptions(request.Options);
                            result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                            var sw = Stopwatch.StartNew();
                            await collectionMgr
                                .CreateCollectionAsync(request.ScopeName, request.Name, settings, options)
                                .ConfigureAwait(false);
                            sw.Stop();
                            result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                            ResultsUtil.SetSuccess(result);
                        }
                            break;
                        case Grpc.Protocol.Sdk.Bucket.CollectionManager.Command.CommandOneofCase.CreateScope:
                        {
                            var request = opCollection.CreateScope;
                            var options = OptionsUtil.ConvertOptions(request.Options);
                            result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                            var sw = Stopwatch.StartNew();
                            await collectionMgr
                                .CreateScopeAsync(request.Name, options)
                                .ConfigureAwait(false);
                            sw.Stop();
                            result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                            ResultsUtil.SetSuccess(result);
                        }
                            break;
                        case Grpc.Protocol.Sdk.Bucket.CollectionManager.Command.CommandOneofCase.DropCollection:
                        {
                            var request = opCollection.DropCollection;
                            var options = OptionsUtil.ConvertOptions(request.Options);
                            result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                            var sw = Stopwatch.StartNew();
                            await collectionMgr.DropCollectionAsync(request.ScopeName, request.Name, options).ConfigureAwait(false);
                            sw.Stop();
                            result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                            ResultsUtil.SetSuccess(result);
                        }
                            break;
                        case Grpc.Protocol.Sdk.Bucket.CollectionManager.Command.CommandOneofCase.DropScope:
                        {
                            var request = opCollection.DropScope;
                            var options = OptionsUtil.ConvertOptions(request.Options);
                            result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                            var sw = Stopwatch.StartNew();
                            await collectionMgr.DropScopeAsync(request.Name, options).ConfigureAwait(false);
                            sw.Stop();
                            result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                            ResultsUtil.SetSuccess(result);
                        }
                            break;
                        case Grpc.Protocol.Sdk.Bucket.CollectionManager.Command.CommandOneofCase.UpdateCollection:
                        {
                            var request = opCollection.UpdateCollection;
                            var settings = OptionsUtil.ConvertSettings(request.Settings);
                            var options = OptionsUtil.ConvertOptions(request.Options);
                            result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                            var sw = Stopwatch.StartNew();
                            await collectionMgr.UpdateCollectionAsync(request.ScopeName, request.Name, settings, options).ConfigureAwait(false);
                            sw.Stop();
                            result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                            ResultsUtil.SetSuccess(result);
                        }
                            break;
                        case Grpc.Protocol.Sdk.Bucket.CollectionManager.Command.CommandOneofCase.GetAllScopes:
                        {
                            var request = opCollection.GetAllScopes;
                            var options = OptionsUtil.ConvertOptions(request.Options);
                            result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                            var sw = Stopwatch.StartNew();
                            var response = await collectionMgr.GetAllScopesAsync(options).ConfigureAwait(false);
                            sw.Stop();
                            result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                            ResultsUtil.PopulateResult(response, result);
                        }
                            break;
                        default:
                            throw new NotSupportedException("Unknown CollectionManager Command");
                    }
                }
                    break;
                case BucketLevelCommand.CommandOneofCase.WaitUntilReady:
                {
                    var bucket = await connection.GetBucketAsync(opBucket.BucketName).ConfigureAwait(false);
                    var request = op.BucketCommand.WaitUntilReady;
                    var timeout = TimeSpan.FromMilliseconds(request.TimeoutMillis);
                    var options = OptionsUtil.ConvertWaitUntilReadyOptions(request.Options);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    await bucket.WaitUntilReadyAsync(timeout, options).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    ResultsUtil.SetSuccess(result);
                }
                    break;
                default:
                    throw new NotSupportedException("Unknown Cluster Command");
            }
        }

        private static async Task HandleClusterLevelCommand(Grpc.Protocol.Sdk.Command op, ClusterConnection connection, Result result)
        {
            switch (op.ClusterCommand.CommandCase)
            {
                case ClusterLevelCommand.CommandOneofCase.Authenticator:
                    switch (op.ClusterCommand.Authenticator.AuthenticatorCase)
                {
                    case Authenticator.AuthenticatorOneofCase.PasswordAuth:
                        result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                        var passwordAuth = op.ClusterCommand.Authenticator.PasswordAuth;
                        var sw = Stopwatch.StartNew();
                        ((Cluster)connection.Cluster)
                            .Authenticator(new PasswordAuthenticator(passwordAuth.Username, passwordAuth.Password));
                        sw.Stop();
                        result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                        ResultsUtil.SetSuccess(result);
                        break;
                    case Authenticator.AuthenticatorOneofCase.CertificateAuth:
                        result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                        sw = Stopwatch.StartNew();
                        ((Cluster)connection.Cluster).Authenticator(ClusterConnection.CreateCertificateAuthenticator(op.ClusterCommand.Authenticator.CertificateAuth));
                        sw.Stop();
                        result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                        ResultsUtil.SetSuccess(result);
                        break;
                    case Authenticator.AuthenticatorOneofCase.JwtAuth:
                        result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                        sw = Stopwatch.StartNew();
                        ((Cluster)connection.Cluster).Authenticator(new JwtAuthenticator(op.ClusterCommand.Authenticator.JwtAuth.Jwt));
                        sw.Stop();
                        result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                        ResultsUtil.SetSuccess(result);
                        break;
                    case Authenticator.AuthenticatorOneofCase.None:
                    default:
                        throw new InvalidArgumentException("Unknown/Unset authenticator type");
                }
                    break;

                case ClusterLevelCommand.CommandOneofCase.QueryIndexManager:
                {
                    switch (op.ClusterCommand.QueryIndexManager.CommandCase)
                    {
                        case Grpc.Protocol.Sdk.Cluster.Query.IndexManager.Command.CommandOneofCase.Shared:
                        {
                            await RunSharedIndexManagementCommand(op.ClusterCommand, connection, result).ConfigureAwait(false);
                            break;
                        }
                        case Grpc.Protocol.Sdk.Cluster.Query.IndexManager.Command.CommandOneofCase.None:
                        {
                            throw new NotImplementedException();
                        }
                        default:
                            throw new NotSupportedException("Unknown Cluster Query Index Command");
                    }
                    break;
                }
                case ClusterLevelCommand.CommandOneofCase.WaitUntilReady:
                {
                    var request = op.ClusterCommand.WaitUntilReady;
                    var timeout = TimeSpan.FromMilliseconds(request.TimeoutMillis);
                    var options = OptionsUtil.ConvertWaitUntilReadyOptions(request.Options);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    await connection.Cluster.WaitUntilReadyAsync(timeout, options).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    ResultsUtil.SetSuccess(result);
                    break;
                }
                case ClusterLevelCommand.CommandOneofCase.Query:
                {
                    await QueryHelper.PerformClusterQuery(op.ClusterCommand.Query, _spans, result, connection.Cluster, op.ReturnResult).ConfigureAwait(false);
                    break;
                }
                case ClusterLevelCommand.CommandOneofCase.Search:
                {
                    await SearchHelper.ExecuteSearchQuery(op.ClusterCommand.Search, _spans, result, connection.Cluster).ConfigureAwait(false);
                    break;
                }
                case ClusterLevelCommand.CommandOneofCase.SearchV2:
                    await SearchHelper.ExecuteSearchV2Query(op.ClusterCommand.SearchV2, _spans, result,
                        connection.Cluster);
                    break;
                case ClusterLevelCommand.CommandOneofCase.BucketManager:
                    var bucketCmd = op.ClusterCommand.BucketManager;
                    var bucketManager = connection.Cluster.Buckets;
                    switch (bucketCmd.CommandCase)
                    {
                        case Grpc.Protocol.Sdk.Cluster.BucketManager.Command.CommandOneofCase.CreateBucket:
                        {
                            var request = bucketCmd.CreateBucket;
                            var settings = OptionsUtil.ConvertSettings(request.Settings.Settings);
                            if (request.Settings.HasConflictResolutionType) settings.ConflictResolutionType = OptionsUtil.ConvertConflictResolutionType(request.Settings.ConflictResolutionType);
                            var options = OptionsUtil.ConvertOptions(request.Options);
                            result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                            var sw = Stopwatch.StartNew();
                            await bucketManager.CreateBucketAsync(settings, options).ConfigureAwait(false);
                            sw.Stop();
                            result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                            ResultsUtil.SetSuccess(result);
                        }
                            break;
                        case Grpc.Protocol.Sdk.Cluster.BucketManager.Command.CommandOneofCase.DropBucket:
                        {
                            var request = bucketCmd.DropBucket;
                            var options = OptionsUtil.ConvertOptions(request.Options);
                            result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                            var sw = Stopwatch.StartNew();
                            await bucketManager.DropBucketAsync(request.BucketName, options).ConfigureAwait(false);
                            sw.Stop();
                            result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                            ResultsUtil.SetSuccess(result);
                        }
                            break;
                        case Grpc.Protocol.Sdk.Cluster.BucketManager.Command.CommandOneofCase.UpdateBucket:
                        {
                            var request = bucketCmd.UpdateBucket;
                            var settings = OptionsUtil.ConvertSettings(request.Settings);
                            var options = OptionsUtil.ConvertOptions(request.Options);
                            result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                            var sw = Stopwatch.StartNew();
                            await bucketManager.UpdateBucketAsync(settings, options).ConfigureAwait(false);
                            sw.Stop();
                            result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                            ResultsUtil.SetSuccess(result);
                        }
                            break;
                        case Grpc.Protocol.Sdk.Cluster.BucketManager.Command.CommandOneofCase.GetAllBuckets:
                        {
                            var request = bucketCmd.GetAllBuckets;
                            var options = OptionsUtil.ConvertOptions(request.Options);
                            result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                            var sw = Stopwatch.StartNew();
                            var response = await bucketManager.GetAllBucketsAsync(options).ConfigureAwait(false);
                            sw.Stop();
                            result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                            ResultsUtil.PopulateResult(response, result);
                        }
                            break;
                        case Grpc.Protocol.Sdk.Cluster.BucketManager.Command.CommandOneofCase.GetBucket:
                        {
                            var request = bucketCmd.GetBucket;
                            var options = OptionsUtil.ConvertOptions(request.Options);
                            result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                            var sw = Stopwatch.StartNew();
                            var response = await bucketManager.GetBucketAsync(request.BucketName, options).ConfigureAwait(false);
                            sw.Stop();
                            result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                            ResultsUtil.PopulateResult(response, result);
                        }
                            break;
                        case Grpc.Protocol.Sdk.Cluster.BucketManager.Command.CommandOneofCase.FlushBucket:
                        {
                            var request = bucketCmd.FlushBucket;
                            var options = OptionsUtil.ConvertOptions(request.Options);
                            result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                            var sw = Stopwatch.StartNew();
                            await bucketManager.FlushBucketAsync(request.BucketName, options).ConfigureAwait(false);
                            sw.Stop();
                            result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                            ResultsUtil.SetSuccess(result);
                        }
                            break;
                        case Grpc.Protocol.Sdk.Cluster.BucketManager.Command.CommandOneofCase.None:
                            break;
                        default:
                            throw new NotSupportedException("Unknown BucketManager command.");
                    }
                    break;
                case ClusterLevelCommand.CommandOneofCase.EventingFunctionManager:
                    throw new NotImplementedException();
                case ClusterLevelCommand.CommandOneofCase.SearchIndexManager:
                {
                    var opSearchIndex = op.ClusterCommand.SearchIndexManager;
                    var indexManager = connection.Cluster.SearchIndexes;
                    switch (opSearchIndex.CommandCase)
                    {
                        case Grpc.Protocol.Sdk.Cluster.Search.IndexManager.Command.CommandOneofCase.Shared:
                        {
                            await SearchIndexManagementHelper.RunSharedSearchIndexManagementCommand(opSearchIndex.Shared, indexManager, result);
                        }
                            break;
                            default:
                            throw new NotSupportedException(
                                "Performer only supports Shared SearchIndexManagement requests.");
                    }
                }
                    break;
                default:
                    throw new NotSupportedException("Unknown Cluster Command: " + op.ClusterCommand.CommandCase);
            }
        }

        private static async Task RunSharedIndexManagementCommand(Couchbase.Grpc.Protocol.Sdk.ClusterLevelCommand op, ClusterConnection connection, Grpc.Protocol.Run.Result result)
        {
            var command = op.QueryIndexManager.Shared.CommandCase;

            switch (command)
            {
                case Command.CommandOneofCase.CreateIndex:
                {
                    var protoRequest = op.QueryIndexManager.Shared.CreateIndex;
                    var opts = OptionsUtil.ConvertOptions(protoRequest.Options);
                    var fields = new List<string>(protoRequest.Fields);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    await connection.Cluster.QueryIndexes.CreateIndexAsync(op.QueryIndexManager.BucketName, protoRequest.IndexName, fields, opts).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    ResultsUtil.SetSuccess(result);
                    break;
                }
                case Command.CommandOneofCase.DropIndex:
                {
                    var protoRequest = op.QueryIndexManager.Shared.DropIndex;
                    var opts = OptionsUtil.ConvertOptions(protoRequest.Options);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    await connection.Cluster.QueryIndexes.DropIndexAsync(op.QueryIndexManager.BucketName, protoRequest.IndexName, opts).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    ResultsUtil.SetSuccess(result);
                    break;
                }
                case Command.CommandOneofCase.WatchIndexes:
                {
                    var protoRequest = op.QueryIndexManager.Shared.WatchIndexes;
                    var opts = OptionsUtil.ConvertOptions(protoRequest.Options);
                    var indexNames = new List<string>(protoRequest.IndexNames);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    await connection.Cluster.QueryIndexes.WatchIndexesAsync(op.QueryIndexManager.BucketName, indexNames, opts).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    ResultsUtil.SetSuccess(result);
                    break;
                }
                case Command.CommandOneofCase.BuildDeferredIndexes:
                {
                    var protoRequest = op.QueryIndexManager.Shared.BuildDeferredIndexes;
                    var opts = OptionsUtil.ConvertOptions(protoRequest.Options);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    await connection.Cluster.QueryIndexes.BuildDeferredIndexesAsync(op.QueryIndexManager.BucketName, opts).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    ResultsUtil.SetSuccess(result);
                    break;
                }
                case Command.CommandOneofCase.CreatePrimaryIndex:
                {
                    var protoRequest = op.QueryIndexManager.Shared.CreatePrimaryIndex;
                    var opts = OptionsUtil.ConvertOptions(protoRequest.Options);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    await connection.Cluster.QueryIndexes.CreatePrimaryIndexAsync(op.QueryIndexManager.BucketName, opts).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    ResultsUtil.SetSuccess(result);
                    break;
                }
                case Command.CommandOneofCase.DropPrimaryIndex:
                {
                    var protoRequest = op.QueryIndexManager.Shared.DropPrimaryIndex;
                    var opts = OptionsUtil.ConvertOptions(protoRequest.Options);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    await connection.Cluster.QueryIndexes.DropPrimaryIndexAsync(op.QueryIndexManager.BucketName, opts).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    ResultsUtil.SetSuccess(result);
                    break;
                }
                case Command.CommandOneofCase.GetAllIndexes:
                {
                    var protoRequest = op.QueryIndexManager.Shared.GetAllIndexes;
                    var opts = OptionsUtil.ConvertOptions(protoRequest.Options);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    var response = await connection.Cluster.QueryIndexes.GetAllIndexesAsync(op.QueryIndexManager.BucketName, opts).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    ResultsUtil.ConvertQueryIndexes(response, result);
                    break;
                }
            }
        }

        private static async Task RunSharedIndexManagementCommand(Couchbase.Grpc.Protocol.Sdk.CollectionLevelCommand op, ICouchbaseCollection collection, Grpc.Protocol.Run.Result result)
        {
            var command = op.QueryIndexManager.Shared.CommandCase;

            //If 3.4.5 doesn't work, try 3.4.12
            switch (command)
            {
                case Command.CommandOneofCase.CreateIndex:
                {
                    var protoRequest = op.QueryIndexManager.Shared.CreateIndex;
                    var opts = OptionsUtil.ConvertOptions(protoRequest.Options);
                    var fields = new List<string>(protoRequest.Fields);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    await collection.QueryIndexes.CreateIndexAsync(protoRequest.IndexName, fields, opts).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    ResultsUtil.SetSuccess(result);
                    break;
                }
                case Command.CommandOneofCase.DropIndex:
                {
                    var protoRequest = op.QueryIndexManager.Shared.DropIndex;
                    var opts = OptionsUtil.ConvertOptions(protoRequest.Options);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    await collection.QueryIndexes.DropIndexAsync(protoRequest.IndexName, opts).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    ResultsUtil.SetSuccess(result);
                    break;
                }
                case Command.CommandOneofCase.WatchIndexes:
                {
                    var protoRequest = op.QueryIndexManager.Shared.WatchIndexes;
                    var opts = OptionsUtil.ConvertOptions(protoRequest.Options);
                    var indexNames = new List<string>(protoRequest.IndexNames);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    await collection.QueryIndexes.WatchIndexesAsync(indexNames, TimeSpan.FromMilliseconds(protoRequest.TimeoutMsecs), opts).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    ResultsUtil.SetSuccess(result);
                    break;
                }
                case Command.CommandOneofCase.BuildDeferredIndexes:
                {
                    var protoRequest = op.QueryIndexManager.Shared.BuildDeferredIndexes;
                    var opts = OptionsUtil.ConvertOptions(protoRequest.Options);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    await collection.QueryIndexes.BuildDeferredIndexesAsync(opts).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    ResultsUtil.SetSuccess(result);
                    break;
                }
                case Command.CommandOneofCase.CreatePrimaryIndex:
                {
                    var protoRequest = op.QueryIndexManager.Shared.CreatePrimaryIndex;
                    var opts = OptionsUtil.ConvertOptions(protoRequest.Options);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    await collection.QueryIndexes.CreatePrimaryIndexAsync(opts).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    ResultsUtil.SetSuccess(result);
                    break;
                }
                case Command.CommandOneofCase.DropPrimaryIndex:
                {
                    var protoRequest = op.QueryIndexManager.Shared.DropPrimaryIndex;
                    var opts = OptionsUtil.ConvertOptions(protoRequest.Options);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    await collection.QueryIndexes.DropPrimaryIndexAsync(opts).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    ResultsUtil.SetSuccess(result);
                    break;
                }
                case Command.CommandOneofCase.GetAllIndexes:
                {
                    var protoRequest = op.QueryIndexManager.Shared.GetAllIndexes;
                    var opts = OptionsUtil.ConvertOptions(protoRequest.Options);
                    result.Initiated = Timestamp.FromDateTime(DateTime.UtcNow);
                    var sw = Stopwatch.StartNew();
                    var response = await collection.QueryIndexes.GetAllIndexesAsync(opts).ConfigureAwait(false);
                    sw.Stop();
                    result.ElapsedNanos = sw.Elapsed.CalculateNanos();
                    ResultsUtil.ConvertQueryIndexes(response, result);
                    break;
                }
            }
        }
    }
}