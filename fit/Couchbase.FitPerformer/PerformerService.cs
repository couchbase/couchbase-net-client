#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Grpc.Protocol.Shared;
using Couchbase.Grpc.Protocol;
using Couchbase.Grpc.Protocol.Performer;
using Couchbase.Grpc.Protocol.Transactions;
using Grpc.Core;
using Couchbase.KeyValue;
using Couchbase.Client.Transactions.Config;
using Couchbase.Client.Transactions.Cleanup;
using Couchbase.Client.Transactions.Components;
using Couchbase.Client.Transactions.DataAccess;
using Couchbase.Client.Transactions.Cleanup.LostTransactions;
using Couchbase.Client.Transactions.Error;
using Couchbase.Client.Transactions;
using Serilog.Extensions.Logging;
using System.Threading;
using System.Runtime.CompilerServices;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.FitPerformer.Utils;
using Couchbase.FitPerformer.Utils.Options;
using Couchbase.FitPerformer.Utils.Results;
using Couchbase.FitPerformer.Workload;
using Couchbase.FitPerformer.Workload.Streams;
using Couchbase.Grpc.Protocol.Observability;
using Couchbase.Grpc.Protocol.Streams;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Couchbase.FitPerformer
{
    public class PerformerServiceImpl : PerformerService.PerformerServiceBase
    {
        private Dictionary<string, ClusterConnection> _connections = new Dictionary<string, ClusterConnection>();
        private object _connectionsLock = new object();

        private readonly ConcurrentDictionary<string, Transactions> _transactionFactory = new ConcurrentDictionary<string, Transactions>();

        private readonly StreamOwner _streamOwner = new StreamOwner();

        private readonly Counters _counters = new Counters();

        private ConcurrentDictionary<string, IRequestSpan> _spans = new ConcurrentDictionary<string, IRequestSpan>();

        private readonly ILoggerFactory? _loggerFactory;

        public PerformerServiceImpl(ILoggerFactory? loggerFactory)
        {
           _loggerFactory = loggerFactory;
        }

        public override async Task<ClusterConnectionCreateResponse> clusterConnectionCreate(ClusterConnectionCreateRequest request, ServerCallContext context)
        {
            try
            {
                LogMethodAndRequest(request);
                var clusterConnectionId = request.ClusterConnectionId;
                var response = new ClusterConnectionCreateResponse();

                Serilog.Log.Debug("clusterConnectionId = {ClusterConnectionId}", clusterConnectionId);

                try
                {
                    var clusterConnection = await ClusterConnection.CreateAsync(request, _loggerFactory).ConfigureAwait(false);

                    lock (_connectionsLock)
                    {
                        _connections[clusterConnectionId] = clusterConnection;
                        response.ClusterConnectionCount = _connections.Count;
                    }
                }
                catch (System.Exception ex)
                {
                    var msg = $"Failed to establish connection: {ex}";

                    Serilog.Log.Error("Failed to establish connection {Exception}", ex);
                    context.Status = new Status(StatusCode.Aborted, msg);
                }

                return response;
            }
            catch (System.Exception err)
            {
                throw MapException(err);
            }
        }

        public override async Task<ClusterConnectionCloseResponse> clusterConnectionClose(ClusterConnectionCloseRequest request, ServerCallContext context)
        {
            try
            {
                LogMethodAndRequest(request);

                ClusterConnection? connection;
                var response = new ClusterConnectionCloseResponse();
                lock (_connectionsLock)
                {
                    if (!_connections.Remove(request.ClusterConnectionId, out connection))
                    {
                        throw new KeyNotFoundException($"Connection with ID {request.ClusterConnectionId} not found.");
                    }
                    response.ClusterConnectionCount = _connections.Count;
                }

                try
                {
                    Serilog.Log.Information("Disposing of the ClusterConnection {ClusterConnectionId}", request.ClusterConnectionId);
                    await connection.DisposeAsync().ConfigureAwait(false);
                }
                catch (System.Exception e)
                {
                    //ignore
                }
                LogMethodAndResponse(response);
                return response;
            }
            catch (System.Exception err)
            {
                throw MapException(err);
            }
        }

        public override Task<PerformerCapsFetchResponse> performerCapsFetch(PerformerCapsFetchRequest request, ServerCallContext context)
        {
            try
            {
                var response = new PerformerCapsFetchResponse();

                foreach (var extension in ProtocolVersion.ExtensionsSupported())
                {
                    if (Enum.TryParse<Grpc.Protocol.Transactions.Caps>(extension.PascalCase, out var cap))
                    {
                        response.TransactionImplementationsCaps.Add(cap);
                    }
                    else
                    {
                        Serilog.Log.Warning("Extension: {Ext} is not recognized by the performer", extension);
                    }
                }

                response.PerformerCaps.Add(Grpc.Protocol.Performer.Caps.KvSupport1);
                response.PerformerCaps.Add(Grpc.Protocol.Performer.Caps.TransactionsSupport1);
                response.PerformerCaps.Add(Grpc.Protocol.Performer.Caps.ClusterConfigCert);
                response.PerformerCaps.Add(Grpc.Protocol.Performer.Caps.Observability1);

                response.TransactionImplementationsCaps.Add(Grpc.Protocol.Transactions.Caps.ExtInsertExisting);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkObservabilityClusterLabels);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkQueryIndexManagement);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkBucketManagement);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkCollectionQueryIndexManagement);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkSearchIndexManagement);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkKv);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkCircuitBreakers);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkKvRangeScan);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkQuery);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.WaitUntilReady);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkLookupIn);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkLookupInReplicas);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkCollectionManagement);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkManagementHistoryRetention);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.Protostellar);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkDocumentNotLocked);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkSearch);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkVectorSearch);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkScopeSearch);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkIndexManagementRfcRevision25);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkSearchRfcRevision11);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkScopeSearchIndexManagement);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkVectorSearchBase64);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkAppTelemetry);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkBucketSettingsNumVbuckets);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkZoneAwareReadFromReplica);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkPrefilterVectorSearch);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkCouchbase2Observability);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkObservabilityRfcRev24);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkStableOtelSemanticConventions);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SupportsAuthenticator);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkSetAuthenticator);
                response.SdkImplementationCaps.Add(Grpc.Protocol.Sdk.Caps.SdkJwt);

                response.SupportedApis.Add(API.Default);
                response.TransactionsProtocolVersion = ProtocolVersion.SupportedVersion.ToString();

                response.PerformerUserAgent = "dotnet";

                response.LibraryVersion = typeof(Cluster).Assembly.GetName().Version?.ToString() ?? "0.0.0";

                return Task.FromResult(response);
            }
            catch (System.Exception err)
            {
                throw MapException(err);
            }
        }

        public override async Task<TransactionsFactoryCreateResponse> transactionsFactoryCreate(TransactionsFactoryCreateRequest request, ServerCallContext context)
        {
            LogMethodAndRequest(request);
            var connection = GetClusterConnection(request.ClusterConnectionId);
            var transactions = await CreateTransaction(request, connection);
            var transactionFactoryRef = Guid.NewGuid().ToString();

            _transactionFactory.TryAdd(transactionFactoryRef, transactions);

            Serilog.Log.Debug("Created TransactionFactory with Ref {Ref}", transactionFactoryRef);

            var response = new TransactionsFactoryCreateResponse { TransactionsFactoryRef = transactionFactoryRef };
            return response;
        }

        public override async Task<TransactionGenericResponse> transactionsFactoryClose(TransactionsFactoryCloseRequest request, ServerCallContext context)
        {
            LogMethodAndRequest(request);
            if (_transactionFactory.TryRemove(request.TransactionsFactoryRef, out var transactions))
            {
                Serilog.Log.Debug("Disposing TransactionFactory with Ref {Ref}", request.TransactionsFactoryRef);
                await transactions.DisposeAsync().ConfigureAwait(false);
            }
            return new TransactionGenericResponse();
        }

        public override async Task<Couchbase.Grpc.Protocol.Transactions.TransactionResult> transactionCreate(TransactionCreateRequest request, ServerCallContext context)
        {
            LogMethodAndRequest(request);
            Serilog.Log.Information("Starting Transaction Test {Name}", request.Name);
            Serilog.Log.Debug("Test: {Request}", request);
            var connection = GetClusterConnection(request.ClusterConnectionId);

            var transactions = connection.Cluster.Transactions;

            var twoWayTransaction = new TwoWayTransaction(context.CancellationToken);
            try
            {
                var result = await twoWayTransaction.Run(transactions, request, connection).ConfigureAwait(false);
                Serilog.Log.Information("Completed Transaction Test {Name}", request.Name);
                return result;
            }
            catch (System.Exception ex)
            {
                Serilog.Log.Warning("Completed Transaction with Exception {Exception}", ex);
                throw new RpcException(new Status(StatusCode.Aborted, ex.Message));
            }
        }

        public override async Task transactionStream(IAsyncStreamReader<TransactionStreamDriverToPerformer> requestStream, IServerStreamWriter<TransactionStreamPerformerToDriver> responseStream, ServerCallContext context)
        {
            LogMethodAndRequest(nameof(requestStream));
            // TODO: Is there a way to trigger OnComplete on the java side more gracefully?  Currently triggers OnError(Cancelled)
            Serilog.Log.Debug("transactionStream: {Info}", context);
            var tref = "NotSet";
            var bp = "NotSet";
            try
            {
                var twoWayTransaction = new TwoWayTransaction(context.CancellationToken);
                var tasks = new List<Task>();
                long readyToStart = 0;
                var moveNext = await requestStream.MoveNext(context.CancellationToken).ConfigureAwait(false);
                while (moveNext)
                {
                    moveNext = false;
                    var d2p = requestStream.Current;
                    Serilog.Log.Debug("{Req}Request: {D2p}", bp, d2p);
                    if (d2p.Create != null)
                    {
                        var connection = GetClusterConnection(d2p.Create.ClusterConnectionId);
                        var req = d2p.Create;
                        bp = req.Name + ": ";
                        await responseStream.WriteAsync(new TransactionStreamPerformerToDriver()
                        {
                            Created = new TransactionCreated()
                        }).ConfigureAwait(false);

                        tref = d2p.Create.TransactionsFactoryRef;
                        var t = Task.Run(async () =>
                        {
                            await Task.Delay(0).ConfigureAwait(false);
                            Serilog.Log.Information("{Req} Created, waiting until told to start", bp);
                            SpinWait.SpinUntil(() => Interlocked.Read(ref readyToStart) > 0 || context.CancellationToken.IsCancellationRequested);
                            context.CancellationToken.ThrowIfCancellationRequested();
                            Serilog.Log.Information("{Req} Starting", bp);

                            var transactions = connection.Cluster.Transactions;
                            Grpc.Protocol.Transactions.TransactionResult result = null;
                            try
                            {
                                result = await twoWayTransaction.Run(transactions, req, connection, responseStream).ConfigureAwait(false);

                                Serilog.Log.Debug("{Req} TwoWay.Run completed.  Writing FinalResult: {Result}, unstagingComplete = {Uc}", bp, result, result.UnstagingComplete);
                            }
                            catch (System.Exception ex)
                            {
                                Serilog.Log.Warning("{Req} TwoWay.Run failed: {Ex}", bp, ex);
                                Couchbase.Client.Transactions.TransactionResult tr = null;
                                if (ex is TransactionFailedException tfe)
                                {
                                    tr = tfe.Result;
                                }

                                result = TxnResultsUtil.CreateResult(tr, ex);
                            }
                            finally
                            {
                                var finalResult = new TransactionStreamPerformerToDriver()
                                {
                                    FinalResult = result
                                };

                                await responseStream.WriteAsync(finalResult).ConfigureAwait(false);

                                Serilog.Log.Information("{Req} Transaction has finished, completing stream and ending thread", bp);
                            }
                        });

                        tasks.Add(t);
                        Serilog.Log.Debug("{Req} Create Task enqueued", bp);
                    }
                    else if (d2p.Start != null)
                    {
                        Serilog.Log.Debug("{Req} Start triggered: {Start}", bp, d2p.Start);
                        Interlocked.Increment(ref readyToStart);
                    }
                    else if (d2p.Broadcast != null)
                    {
                        Serilog.Log.Debug("{Req}Broadcast triggered: {Start}", bp, d2p.Broadcast);
                        var req = d2p.Broadcast;
                        if (req.LatchSet != null)
                        {
                            twoWayTransaction.HandleRequest(req.LatchSet);
                        }
                        else
                        {
                            throw new InternalPerformerException("Unknown broadcast request from driver " + requestStream,
                                new NotSupportedException(nameof(d2p.Broadcast)));
                        }
                    }
                    else
                    {
                        throw new InternalPerformerException("Unknown request from driver " + d2p,
                            new NotSupportedException("Unknown request from driver"));
                    }

                    Serilog.Log.Debug("{Req}END: {D2p}", bp, d2p);

                    while (!moveNext && !context.CancellationToken.IsCancellationRequested)
                    {
                        using var ctsCheckStatus = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                        var linkedMoveNext = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, ctsCheckStatus.Token);
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        moveNext = await requestStream.MoveNext(linkedMoveNext.Token).ConfigureAwait(false);
                        var allFinished = tasks.All(t => t.IsCompleted);
                        Serilog.Log.Debug("{Req}MoveNext took {Elapsed}ms with MoveNext={Result}, context.IsCancelled={ContextCancelled}, allFinished={AllFinished}", bp, sw.ElapsedMilliseconds, moveNext, context.CancellationToken.IsCancellationRequested, allFinished);
                        if (!moveNext && !context.CancellationToken.IsCancellationRequested)
                        {
                            Serilog.Log.Debug("{Req}Iteration ended successfully before context was cancelled.");
                            break;
                        }
                    }
                }

                Serilog.Log.Debug("{Req} Done with requestStream. (moveNext = {Mn}, cancelled={C})", bp, moveNext, context.CancellationToken.IsCancellationRequested);
                await Task.WhenAll(tasks).ConfigureAwait(false);
                Serilog.Log.Debug("{Req} Done with background tasks", bp);
                if (requestStream is IDisposable disposeAble)
                {
                    Serilog.Log.Information("{Req} Disposing requestStream", bp);
                    disposeAble.Dispose();
                }

                if (responseStream is IDisposable outDispose)
                {
                    Serilog.Log.Information("{Req} Disposing responseStream", bp);
                    outDispose.Dispose();
                }
            }
            catch (System.Exception ex)
            {
                Serilog.Log.Warning("{Req} Completed Transaction Stream with Exception {Exception}", bp, ex);
                throw new RpcException(new Status(StatusCode.Aborted, ex.Message));
            }

            Serilog.Log.Debug("{Req} Reached end of {Method} with cancellation={Cancellation}, status={Status}", bp, nameof(transactionStream), context.CancellationToken.IsCancellationRequested, context.Status);
        }

        public override async Task<Grpc.Protocol.Transactions.TransactionCleanupAttempt> transactionCleanup(TransactionCleanupRequest request, ServerCallContext context)
        {
            LogMethodAndRequest(request);

            try
            {
                var emptyDocRecords = new List<DocRecord>(0);
                var connection = GetClusterConnection(request.ClusterConnectionId);
                var cleaner = new Cleaner(connection.Cluster, null, new SerilogLoggerFactory());
                cleaner.TestHooks = HooksUtil.ConfigureHooks(request.Hook, connection) ?? cleaner.TestHooks;
                var atrCollection =
                    await connection.GetCollectionAsync(request.Atr.BucketName, request.Atr.ScopeName, request.Atr.CollectionName);

                var atrEntry = await AtrRepository.FindEntryForTransaction(atrCollection, request.Atr.DocId_, request.AttemptId);

                if (atrEntry == null)
                {
                    return new Grpc.Protocol.Transactions.TransactionCleanupAttempt()
                    {
                        AttemptId = request.AttemptId,
                        Atr = request.Atr,
                        State = (AttemptStates)(-1),
                        Success = false
                    };
                }

                var cleanupRequest = new CleanupRequest(
                    AttemptId: request.AttemptId,
                    AtrId: request.Atr.DocId_,
                    AtrCollection: atrCollection,
                    InsertedIds: atrEntry.InsertedIds?.ToList() ?? emptyDocRecords,
                    ReplacedIds: atrEntry.ReplacedIds?.ToList() ?? emptyDocRecords,
                    RemovedIds: atrEntry.RemovedIds?.ToList() ?? emptyDocRecords,
                    State: atrEntry.State,
                    WhenReadyToBeProcessed: DateTimeOffset.UtcNow,
                    ProcessingErrors: new ConcurrentQueue<System.Exception>(),
                    ForwardCompatibility: atrEntry.ForwardCompatibility);

                var cleanupResult = await cleaner.ProcessCleanupRequest(cleanupRequest);

                atrEntry = await AtrRepository.FindEntryForTransaction(atrCollection, request.Atr.DocId_, request.AttemptId);

                return new Grpc.Protocol.Transactions.TransactionCleanupAttempt()
                {
                    Atr = request.Atr,
                    AttemptId = cleanupResult.AttemptId,
                    State = (AttemptStates)(atrEntry?.State ?? Couchbase.Client.Transactions.Support.AttemptStates.UNKNOWN),
                    Success = cleanupResult.Success
                };
            }
            catch (System.Exception e)
            {
                Serilog.Log.Error("Cleanup FAILED for {Atr}/{AttemptId}: {Reason}", request.Atr.DocId_, request.AttemptId, e);
                var failureResult = new Grpc.Protocol.Transactions.TransactionCleanupAttempt()
                {
                    Atr = request.Atr,
                    AttemptId = request.AttemptId,
                    State = AttemptStates.Unknown,
                    Success = false
                };

                failureResult.Logs.Add(e.ToString());
                return failureResult;
            }
        }

        public override Task<TransactionCleanupATRResult> transactionCleanupATR(TransactionCleanupATRRequest request, ServerCallContext context)
        {
            LogMethodAndRequest(request);
            Serilog.Log.Error("Unimplemented: {Method}, Request: {Request}", nameof(transactionCleanupATR), request);
            return base.transactionCleanupATR(request, context);
        }

        public override Task<CleanupSetFetchResponse> cleanupSetFetch(CleanupSetFetchRequest request,
            ServerCallContext context)
        {
            LogMethodAndRequest(request);
            var connection = GetClusterConnection(request.ClusterConnectionId);
            LostTransactionManager? txnManager = connection.Cluster.Transactions._lostTransactionsCleanup as LostTransactionManager;
            var response = new CleanupSetFetchResponse();
            if (txnManager == null) return Task.FromResult(response);
            var collections = txnManager.CollectionsBeingCleaned;
            Serilog.Log.Information("{CollectionsCount} collections to be cleaned", collections.Count);
            var cleanupSet = new Grpc.Protocol.Transactions.CleanupSet();
            foreach (var keyspace in collections)
            {
                Serilog.Log.Information("keyspace being cleaned: {Keyspace}", keyspace);
                var coll = new Grpc.Protocol.Shared.Collection()
                {
                    BucketName = keyspace.BucketName,
                    CollectionName = keyspace.CollectionName,
                    ScopeName = keyspace.ScopeName,
                };

                try
                {
                    cleanupSet.CleanupSet_.Add(coll);
                }
                catch (System.Exception e)
                {
                    Serilog.Log.Error(e, "Cleanup set FAILED");
                }
            }
            response.CleanupSet = cleanupSet;
            LogMethodAndResponse(response);
            return Task.FromResult(response);
        }


        public override async Task<ClientRecordProcessResponse> clientRecordProcess(ClientRecordProcessRequest request,
            ServerCallContext context)
        {
            LogMethodAndRequest(request);

            ClientRecordDetails? clientRecordDetails = null;
            var success = false;
            try
            {
                var connection = GetClusterConnection(request.ClusterConnectionId);
                var cleaner = new Cleaner(connection.Cluster, null, new SerilogLoggerFactory());
                var collection =
                    await connection.GetCollectionAsync(request.BucketName, request.ScopeName, request.CollectionName);
                var cleanupWindow = TimeSpan.FromSeconds(2.5);
                var repo = new CleanerRepository(collection, null);

                var perBucketCleaner = new PerCollectionCleaner(request.ClientUuid, cleaner, repo, cleanupWindow,
                    new SerilogLoggerFactory(), startDisabled: true);
                perBucketCleaner.TestHooks =
                    HooksUtil.ConfigureHooks(request.Hook, connection) ?? perBucketCleaner.TestHooks;

                clientRecordDetails = await perBucketCleaner.ProcessClient(cleanupAtrs: false);
                Serilog.Log.Debug("Client Record From Run: {Crb}", clientRecordDetails);
                success = true;

            }
            catch (System.Exception e)
            {
                Log.Error("Client record processing FAILED: {Msg}", e.Message);
            }

            ClientRecordProcessResponse result;

            if (clientRecordDetails != null)
            {
                result = new ClientRecordProcessResponse()
                {
                    ClientUuid = request.ClientUuid,
                    CasNowNanos = clientRecordDetails.CasNowNanos,
                    IndexOfThisClient = clientRecordDetails.IndexOfThisClient,
                    NumActiveClients = clientRecordDetails.NumActiveClients,
                    NumExistingClients = clientRecordDetails.NumExistingClients,
                    NumExpiredClients = clientRecordDetails.NumExpiredClients,
                    OverrideActive = clientRecordDetails.OverrideActive,
                    OverrideEnabled = clientRecordDetails.OverrideEnabled,
                    OverrideExpires = (clientRecordDetails.OverrideExpires?.ToUnixTimeMilliseconds() ?? 0) *
                                      ClientRecordDetails.NanosecondsPerMillisecond,
                    Success = success,
                };

                foreach (var clientId in clientRecordDetails!.ExpiredClientIds)
                {
                    result.ExpiredClientIds.Add(clientId);
                }
            }
            else
            {
                result = new ClientRecordProcessResponse()
                {
                    ClientUuid = request.ClientUuid,
                    Success = false
                };
            }

            LogMethodAndResponse(result);
            return result;
        }

        public override Task<EchoResponse> echo(EchoRequest request, ServerCallContext context)
        {
            Serilog.Log.Information("======== {TestName}: {Message} =======", request.TestName, request.Message);
            return Task.FromResult(new EchoResponse());
        }

        private async Task<Transactions> CreateTransaction(TransactionsFactoryCreateRequest request, ClusterConnection connection)
        {
            // NOTE: this is only called for createTransactionFactory, which goes away with
            // extSdkIntegration (3.6.6).  SO...
            var configBuilder = TransactionsConfigBuilder.Create();

            var txnConfig = request.Config;
            var cleanupConfig = txnConfig.CleanupConfig;
            // we can ignore this if 3.6.6 since it will never be called...
            if (txnConfig.HasDurability)
            {
                var durabilityLevel = MapDurability(txnConfig.Durability);
                if (durabilityLevel != null)
                {
                    configBuilder.DurabilityLevel(durabilityLevel.Value);
                }
            }

            if (txnConfig.HasTimeoutMillis)
            {
                configBuilder.ExpirationTime(TimeSpan.FromMilliseconds(txnConfig.TimeoutMillis));
            }

            var mdConfig = txnConfig.MetadataCollection;
            if (mdConfig != null)
            {
                configBuilder.MetadataCollection(TxnOptionsUtil.ConvertCollectionToKeyspace(mdConfig));
            }

            var queryConfig = txnConfig.QueryConfig;
            if (queryConfig != null)
            {
                TransactionQueryConfigBuilder queryConfigBuilder = new();
                if (queryConfig.HasScanConsistency
                    && Enum.TryParse<Query.QueryScanConsistency>(queryConfig.ScanConsistency.ToString(), out var parsedScanConsistency))
                {
                    queryConfigBuilder.ScanConsistencyValue = parsedScanConsistency;
                }
                configBuilder.QueryConfig(queryConfigBuilder);
            }

            var transactions = Transactions.Create(connection.Cluster, configBuilder.Build());
            HooksUtil.ConfigureHooks(request.Config.Hook, GetClusterConnection(request.ClusterConnectionId), transactions);
            return transactions;
        }

        public override async Task<CloseTransactionsResponse> closeTransactions(CloseTransactionsRequest request, ServerCallContext context)
        {
            LogMethodAndRequest(request);
            var txns = _transactionFactory.Values.ToList();
            txns.Clear();
            foreach (var txn in txns)
            {
                try
                {
                    await txn.DisposeAsync();
                }
                catch (System.Exception ex)
                {
                    Serilog.Log.Warning("Dispose Transaction failed: {Cause}", ex);
                }
            }

            return new();
        }

        public override async Task<DisconnectConnectionsResponse> disconnectConnections(DisconnectConnectionsRequest request, ServerCallContext context)
        {
            LogMethodAndRequest(request);
            List<ClusterConnection> conns = new();
            lock (_connectionsLock)
            {
                conns = _connections.Values.ToList();
                _connections.Clear();
            }

            foreach (var conn in conns)
            {
                try
                {
                    await conn.DisposeAsync().ConfigureAwait(false);
                }
                catch (System.Exception ex)
                {
                    Serilog.Log.Warning("Close Connection failed: {Cause}", ex);
                }
            }

            return new();
        }

        public override async Task<TransactionSingleQueryResponse> transactionSingleQuery(TransactionSingleQueryRequest request, ServerCallContext context)
        {
            LogMethodAndRequest(request);
            try
            {
                var connection = GetClusterConnection(request.ClusterConnectionId);
                var transactions = connection.Cluster.Transactions;

                IScope scope = null;
                if (request.Query?.Scope != null)
                {
                    var qs = request.Query.Scope;
                    var bucket = await connection.GetBucketAsync(qs.BucketName);
                    scope = bucket.Scope(qs.ScopeName);
                }

                SingleQueryTransactionConfigBuilder singleQueryConfig = null;
                var ropts = request.QueryOptions;
                var handledProps = new HashSet<string>();
                if (ropts != null) {
                    singleQueryConfig = new SingleQueryTransactionConfigBuilder();

                    if (ropts.SingleQueryTransactionOptions != null) {
                        if (ropts.SingleQueryTransactionOptions.HasDurability) {
                            var durabilityLevel = MapDurability(ropts.SingleQueryTransactionOptions.Durability);
                            if (durabilityLevel != null) {
                                singleQueryConfig.DurabilityLevel(durabilityLevel.Value);
                            }
                        }

                        handledProps.Add(nameof(ropts.SingleQueryTransactionOptions.HasDurability));

                        HooksUtil.ConfigureHooks(ropts.SingleQueryTransactionOptions.Hook, connection);
                    }

                    if (request.QueryOptions.HasTimeoutMillis) {
                        singleQueryConfig.Timeout(TimeSpan.FromMilliseconds(ropts.TimeoutMillis));
                    }

                    handledProps.Add(nameof(ropts.HasTimeoutMillis));

                    // the GRPC Optional conversion leads to a lot of boilerplate.
                    // The reflection here is a catch-all, but may not actually be accurate.
                    // As far as I can tell, these aren't actually being used in the tests yet.
                    singleQueryConfig.QueryOptions(opts =>
                    {
                        var requestOptionsType = ropts.GetType();
                        var queryOptionsType = opts.GetType();
                        var hasProps = requestOptionsType.GetProperties().Where(p => p.Name.StartsWith("Has") && !handledProps.Contains(p.Name));
                        foreach (var hasProp in hasProps)
                        {
                            if (hasProp.GetValue(ropts) is not true)
                            {
                                continue;
                            }

                            var hasName = hasProp.Name;
                            var propName = hasName[3..];
                            var prop = requestOptionsType.GetProperty(propName);
                            var method = queryOptionsType.GetMethod(propName);
                            if (prop != null && method != null)
                            {
                                var propVal = prop.GetValue(ropts);
                                method.Invoke(opts, new object[] { propVal });
                            }
                        }
                    });
                }

                // TODO:  API docs updated to have non-void return for tximplicit.
                var singleQueryResult = await transactions.QueryAsync<object>(request.Query.Statement, singleQueryConfig, scope);
                await ResultValidation.ValidateQueryResult(request.Query, singleQueryResult?.QueryResult);
                var queryResponse = new TransactionSingleQueryResponse();
                queryResponse.Log.AddRange(singleQueryResult.Logs);
                return queryResponse;
            }
            catch (TransactionFailedException err)
            {
                Serilog.Log.Information("Got TransactionFailed.  Creating the result: {Cause}", err.InnerException);
                var response = new TransactionSingleQueryResponse()
                {
                    Exception = TxnResultsUtil.ConvertTransactionFailed(err),
                    ExceptionCause = TxnResultsUtil.MapCause(err.InnerException ?? err),
                };

                if (err.Result?.Logs != null)
                {
                    response.Log.AddRange(err.Result.Logs);
                }

                return response;
            }
            catch (CouchbaseException err)
            {
                var response = new TransactionSingleQueryResponse()
                {
                    Exception = TxnResultsUtil.ConvertTransactionFailed(err),
                    ExceptionCause = TxnResultsUtil.MapCause(err),
                };

                return response;
            }
            catch (System.Exception err)
            {
                Serilog.Log.Error("Operation failed during {Method} due to {Cause}", nameof(transactionSingleQuery), err);
                throw new RpcException(new Status(StatusCode.Aborted, err.ToString()));
            }
        }

        public override async Task run(Couchbase.Grpc.Protocol.Run.Request request, IServerStreamWriter<Couchbase.Grpc.Protocol.Run.Result> responseStream, ServerCallContext context)
        {
            try {
                if (request.RequestCase != Grpc.Protocol.Run.Request.RequestOneofCase.Workloads) {
                    throw new NotSupportedException();
                }

                var runId = Guid.NewGuid().ToString();
                var cts = new CancellationTokenSource();
                var channel = System.Threading.Channels.Channel.CreateUnbounded<Couchbase.Grpc.Protocol.Run.Result>();
                var config = request.Config;
                var connection = GetClusterConnection(request.Workloads.ClusterConnectionId);

                long itemsProduced = 0;
                long itemsConsumed = 0;

                // These two should not be awaited here, as we do not want them to be running on the same thread as
                // WorkloadRunExecutor.RunWorkloads() awaited below.
                var resultStreamingTask = Task.Run(async () => {
                    await WorkloadStreamingThread.ConsumeResultsAsync(responseStream,
                        config,
                        cts.Token,
                        channel,
                        () => Interlocked.Increment(ref itemsConsumed));
                }, cts.Token);

                _ = Task.Run(async () => {
                    await SystemMetricsReporter.StartReportingAsync(channel, config, cts.Token);
                }, cts.Token);

                await WorkloadRunExecutor.RunWorkloadsAsync(
                    request.Workloads,
                    async (x) =>
                    {
                        await channel.Writer.WriteAsync(x, CancellationToken.None).ConfigureAwait(false);
                        Interlocked.Increment(ref itemsProduced);
                    },
                    _counters,
                    connection,
                    runId,
                    _streamOwner,
                    _spans).ConfigureAwait(false);

                var breakLimit = 0; // Break condition to avoid infinite loop
                // We don't want to cancel the Token as soon as the producer finishes since the consumer might still need to read and send the last items.
                while (itemsConsumed < itemsProduced)
                {
                    if (breakLimit % 10 == 0) Serilog.Log.Debug("Items produced: {ItemsProduced}, Items consumed: {ItemsConsumed}", itemsProduced, itemsConsumed);
                    await Task.Delay(1).ConfigureAwait(false);
                    breakLimit++;
                    if (breakLimit > 1000) break;
                }
                cts.Cancel();
                await resultStreamingTask.ConfigureAwait(false);
            }
            catch (System.Exception err) {
                throw MapException(err);
            }
        }

        public override Task<CancelResponse> streamCancel(CancelRequest request, ServerCallContext context)
        {
            try
            {
                _streamOwner.Cancel(request);
                Serilog.Log.Debug("Requested Cancellation on Stream #{StreamId}", request.StreamId);
                return Task.FromResult(new CancelResponse());
            }
            catch (NotSupportedException notSupportedException)
            {
                Serilog.Log.Debug(notSupportedException, "Error in Stream CancelRequest : Not supported");
                return Task.FromException<CancelResponse>(notSupportedException);
            }
        }

        public override Task<RequestItemsResponse> streamRequestItems(RequestItemsRequest request, ServerCallContext context)
        {
            try
            {
                _streamOwner.RequestItems(request);
                Serilog.Log.Debug("Requested {N} items on Stream #{StreamId}", request.NumItems, request.StreamId);
                return Task.FromResult(new RequestItemsResponse());
            }
            catch (NotSupportedException notSupportedException)
            {
                Serilog.Log.Debug(notSupportedException, "Error in Stream RequestItems : Not supported");
                return Task.FromException<RequestItemsResponse>(notSupportedException);
            }
        }

        public override Task<SetCounterResponse> setCounter(Couchbase.Grpc.Protocol.Shared.Counter request, ServerCallContext context)
        {
            try
            {
                LogMethodAndRequest(request);
                _counters.SetCounter(request.CounterId, request.Global.Count);
                return Task.FromResult(new SetCounterResponse());
            }
            catch (System.Exception err)
            {
                throw MapException(err);
            }
        }

        public override Task<ClearAllCountersResponse> clearAllCounters(ClearAllCountersRequest request, ServerCallContext context)
        {
            try
            {
                LogMethodAndRequest(request);
                _counters.ClearCounters();
                return Task.FromResult(new ClearAllCountersResponse());
            }
            catch (System.Exception err)
            {
                throw MapException(err);
            }
        }

        public override Task<SpanCreateResponse> spanCreate(SpanCreateRequest request, ServerCallContext context)
        {
            var parent = request.HasParentSpanId
                ? _spans[request.ParentSpanId]
                : null;

            var requestTracer = GetClusterConnection(request.ClusterConnectionId).Cluster.ClusterServices.GetService(typeof(IRequestTracer)) as IRequestTracer;
            var span = requestTracer?.RequestSpan(request.Name, parent);

            foreach (var (x, y) in request.Attributes)
            {
                switch (y.ValueCase)
                {
                    case Couchbase.Grpc.Protocol.Observability.Attribute.ValueOneofCase.ValueBoolean:
                        span?.SetAttribute(x, y.ValueBoolean);
                        break;
                    case Couchbase.Grpc.Protocol.Observability.Attribute.ValueOneofCase.ValueLong:
                        span?.SetAttribute(x, (uint)y.ValueLong);
                        break;
                    case Couchbase.Grpc.Protocol.Observability.Attribute.ValueOneofCase.ValueString:
                        span?.SetAttribute(x, y.ValueString);
                        break;
                    default:
                        throw new ArgumentException("Error in converting span attributes.");
                }
            }
            _spans.TryAdd(request.Id, span);
            return Task.FromResult(new SpanCreateResponse());
        }

        public override Task<SpanFinishResponse> spanFinish(SpanFinishRequest request, ServerCallContext context)
        {
            _spans[request.Id].End();
            _spans.TryRemove(request.Id, out _);
            return Task.FromResult<SpanFinishResponse>(new SpanFinishResponse());
        }

        private RpcException MapException(System.Exception err)
        {
            Serilog.Log.Warning("Failed with {Err}", err);

            if (err.GetType().IsAssignableTo(typeof(NotSupportedException))) {
                return new RpcException(new Status(StatusCode.Unimplemented, err.Message));
            }

            return new RpcException(new Status(StatusCode.Unknown, err.Message));
        }

        private ClusterConnection GetClusterConnection(string? connectionId)
        {
            lock (_connectionsLock)
            {
                Serilog.Log.Debug("clusterConnectionId:{Id}", connectionId);

                if (_connections.TryGetValue(connectionId!, out var clusterConnection))
                {
                    Serilog.Log.Debug("Using custom connection at host : {Host} and with username {User}", clusterConnection.Hostname, clusterConnection.Username);
                    return clusterConnection;
                }
                Log.Debug("Attempt to get connection {ConnectionId} that was not present", connectionId);
                throw new ArgumentOutOfRangeException(connectionId, $"No such connection setup for id={connectionId}");
            }
        }

        private DurabilityLevel? MapDurability(Durability grpcDurability) => grpcDurability switch
        {
            Durability.Majority => DurabilityLevel.Majority,
            Durability.MajorityAndPersistToActive => DurabilityLevel.MajorityAndPersistToActive,
            Durability.PersistToMajority => DurabilityLevel.PersistToMajority,
            Durability.None => DurabilityLevel.None,
            _ => Enum.TryParse<DurabilityLevel>(grpcDurability.ToString(), out var parsedDurability) ? parsedDurability : null
        };

        private static void LogMethodAndRequest(object request, [CallerMemberName] string method = "UNKNOWN")
        {
            Serilog.Log.Information("{Method}: {Request}", method, request);
        }
        private static void LogMethodAndResponse(object response, [CallerMemberName] string method = "UNKNOWN")
        {
            Serilog.Log.Information("{Method}: response: {Response}", method, response);
        }
    }
}
