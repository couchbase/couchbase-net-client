#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;
using Couchbase.Query;
using Couchbase.Client.Transactions.Cleanup;
using Couchbase.Client.Transactions.Cleanup.LostTransactions;
using Couchbase.Client.Transactions.Config;
using Couchbase.Client.Transactions.DataAccess;
using Couchbase.Client.Transactions.Error;
using Couchbase.Client.Transactions.Error.External;
using Couchbase.Client.Transactions.Internal.Test;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Couchbase.Client.Transactions
{
    /// <summary>
    /// A class for running transactional operations against a Couchbase Cluster.
    /// </summary>
    public class Transactions : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// A standard delay between retried operations.
        /// </summary>
        public static readonly TimeSpan OpRetryDelay = TimeSpan.FromMilliseconds(3);

        internal static readonly ITypeSerializer MetadataSerializer = new DefaultSerializer(
            deserializationSettings: new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),

                DateParseHandling = DateParseHandling.DateTimeOffset
            },
            serializerSettings: new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),

                DateParseHandling = DateParseHandling.DateTimeOffset
            });

        internal static readonly ITypeTranscoder MetadataTranscoder = new JsonTranscoder(MetadataSerializer);

        private static long InstancesCreated;
        private static long InstancesCreatedDoingBackgroundCleanup;
        private readonly ICluster _cluster;
        private readonly IRedactor _redactor;
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger<Transactions> _logger;
        private readonly CleanupWorkQueue _cleanupWorkQueue;
        private readonly Cleaner _cleaner;
        internal readonly IAsyncDisposable? _lostTransactionsCleanup;
        private readonly IRequestTracer _requestTracer;
        private readonly object _syncObject = new();
        private bool _disposed = false;



        /// <summary>
        /// Gets the <see cref="TransactionsConfig"/> to apply to all transaction runs from this instance.
        /// </summary>
        public TransactionsConfig Config { get; }

        internal ICluster Cluster => _cluster;

        internal ITestHooks TestHooks { get; set; } = DefaultTestHooks.Instance;
        internal IDocumentRepository? DocumentRepository { get; set; } = null;
        internal IAtrRepository? AtrRepository { get; set; } = null;
        internal int? CleanupQueueLength => Config.CleanupConfig.CleanupClientAttempts ?_cleanupWorkQueue.QueueLength : null;

        internal ICleanupTestHooks CleanupTestHooks
        {
            get => _cleaner.TestHooks;
            set
            {
                _cleaner.TestHooks = value;
                _cleanupWorkQueue.TestHooks = value;
            }
        }

        internal void ConfigureTestHooks(ITestHooks testHooks, ICleanupTestHooks cleanupHooks)
        {
            TestHooks = testHooks;
            CleanupTestHooks = cleanupHooks;
        }

        private Transactions(ICluster cluster, TransactionsConfig config)
        {
            _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _redactor = _cluster.ClusterServices.GetService(typeof(IRedactor)) as IRedactor ?? throw new ArgumentNullException(nameof(IRedactor), "Redactor implementation not registered.");
            _requestTracer = cluster.ClusterServices.GetService(typeof(IRequestTracer)) as IRequestTracer ?? new NoopRequestTracer();
            Interlocked.Increment(ref InstancesCreated);
            if (config.CleanupConfig.CleanupLostAttempts)
            {
                Interlocked.Increment(ref InstancesCreatedDoingBackgroundCleanup);
            }

            loggerFactory = config.LoggerFactory
                ?? _cluster.ClusterServices.GetService(typeof(ILoggerFactory)) as ILoggerFactory
                ?? NullLoggerFactory.Instance;
            _logger = loggerFactory.CreateLogger<Transactions>();

            _cleanupWorkQueue = new CleanupWorkQueue(_cluster, Config.KeyValueTimeout,loggerFactory, config.CleanupConfig.CleanupClientAttempts);

            _cleaner = new Cleaner(cluster, Config.KeyValueTimeout, loggerFactory, creatorName: nameof(Transactions));

            _logger.LogInformation($"Creating new Transactions instance, {InstancesCreated} created");
            if (!config.CleanupConfig.CleanupLostAttempts) return;
            _logger.LogInformation($"Transactions creating new LostTransactionManager");
            var collections = config.CleanupConfig.CollectionsList;
            if (config.MetadataCollection != null)
            {
                collections.Add(config.MetadataCollection);
            }
            _lostTransactionsCleanup = new LostTransactionManager(_cluster, loggerFactory, config.CleanupConfig.CleanupWindow, config.KeyValueTimeout, collections: collections);

            // TODO: whatever the equivalent of 'cluster.environment().eventBus().publish(new TransactionsStarted(config));' is.
        }

        /// <summary>
        /// Create a <see cref="Transactions"/> instance for running transactions against the specified <see cref="ICluster">Cluster</see>.
        /// </summary>
        /// <param name="cluster">The cluster where your documents will be located.</param>
        /// <returns>A <see cref="Transactions"/> instance.</returns>
        /// <remarks>The instance returned from this method should be kept for the lifetime of your application and used like a singleton per Couchbase cluster you will be accessing.</remarks>
        public static Transactions Create(ICluster cluster) => Create(cluster, TransactionsConfigBuilder.Create().Build());

        /// <summary>
        /// Create a <see cref="Transactions"/> instance for running transactions against the specified <see cref="ICluster">Cluster</see>.
        /// </summary>
        /// <param name="cluster">The cluster where your documents will be located.</param>
        /// <param name="config">The <see cref="TransactionsConfig"/> to use for all transactions against this cluster.</param>
        /// <returns>A <see cref="Transactions"/> instance.</returns>
        /// <remarks>The instance returned from this method should be kept for the lifetime of your application and used like a singleton per Couchbase cluster you will be accessing.</remarks>
        public static Transactions Create(ICluster cluster, TransactionsConfig config) => new Transactions(cluster, config);

        /// <summary>
        /// Create a <see cref="Transactions"/> instance for running transactions against the specified <see cref="ICluster">Cluster</see>.
        /// </summary>
        /// <param name="cluster">The cluster where your documents will be located.</param>
        /// <param name="configBuilder">The <see cref="TransactionsConfigBuilder"/> to generate a <see cref="TransactionsConfig"/> to use for all transactions against this cluster.</param>
        /// <returns>A <see cref="Transactions"/> instance.</returns>
        /// <remarks>The instance returned from this method should be kept for the lifetime of your application and used like a singleton per Couchbase cluster you will be accessing.</remarks>
        public static Transactions Create(ICluster cluster, TransactionsConfigBuilder configBuilder) =>
            Create(cluster, configBuilder.Build());

        /// <summary>
        /// Run a transaction against the cluster.
        /// </summary>
        /// <param name="transactionLogic">A func representing the transaction logic. All data operations should use the methods on the <see cref="AttemptContext"/> provided.  Do not mix and match non-transactional data operations.</param>
        /// <returns>The result of the transaction.</returns>
        public Task<TransactionResult> RunAsync(Func<AttemptContext, Task> transactionLogic) =>
            RunAsync(transactionLogic, PerTransactionConfigBuilder.Create().Build());

        /// <summary>
        /// Run a transaction against the cluster.
        /// </summary>
        /// <param name="transactionLogic">A func representing the transaction logic. All data operations should use the methods on the <see cref="AttemptContext"/> provided.  Do not mix and match non-transactional data operations.</param>
        /// <param name="perConfig">A config with values unique to this specific transaction.</param>
        /// <returns>The result of the transaction.</returns>
        public Task<TransactionResult> RunAsync(Func<AttemptContext, Task> transactionLogic, PerTransactionConfig perConfig) => RunAsync(transactionLogic, perConfig, false);

        internal async Task<TransactionResult> RunAsync(Func<AttemptContext, Task> transactionLogic, PerTransactionConfig perConfig, bool singleQueryTransactionMode)
        {
            // https://hackmd.io/foGjnSSIQmqfks2lXwNp8w?view#The-Core-Loop

            var txId = Guid.NewGuid().ToString();
            using var rootSpan = _requestTracer.RequestSpan(nameof(RunAsync))
                .SetAttribute(Support.TransactionFields.TransactionId, txId);
            var overallContext = new TransactionContext(
                transactionId: txId,
                startTime: DateTimeOffset.UtcNow,
                config: Config,
                perConfig: perConfig
                );

            var result = new TransactionResult() { TransactionId =  overallContext.TransactionId };
            var opRetryBackoffMillisecond = 1;
            var randomJitter = new Random();

            while (true)
            {
                try
                {
                    try
                    {
                        await ExecuteApplicationLambda(transactionLogic, overallContext, loggerFactory, result, rootSpan, singleQueryTransactionMode).CAF();
                        return result;
                    }
                    catch (TransactionOperationFailedException ex)
                    {
                        // If anything above fails with error err
                        if (ex.RetryTransaction && !overallContext.IsExpired)
                        {
                            // If err.retry is true, and the transaction has not expired
                            //Apply OpRetryBackoff, with randomized jitter. E.g.each attempt will wait exponentially longer before retrying, up to a limit.
                            var jitter = randomJitter.Next(10);
                            var delayMs = opRetryBackoffMillisecond + jitter;
                            await Task.Delay(delayMs).CAF();
                            opRetryBackoffMillisecond = Math.Min(opRetryBackoffMillisecond * 10, 100);
                            //    Go back to the start of this loop, e.g.a new attempt.
                            continue;
                        }

                        // Otherwise, we are not going to retry. What happens next depends on err.raise
                        switch (ex.FinalErrorToRaise)
                        {
                            //  Failure post-commit may or may not be a failure to the application,
                            // as the cleanup process should complete the commit soon. It often depends on
                            // whether the application wants RYOW, e.g. AT_PLUS. So, success will be returned,
                            // but TransactionResult.unstagingComplete() will be false.
                            // The application can interpret this as it needs.
                            case TransactionOperationFailedException.FinalError.TransactionFailedPostCommit:
                                result.UnstagingComplete = false;
                                return result;

                            // Raise TransactionExpired to application, with a cause of err.cause.
                            case TransactionOperationFailedException.FinalError.TransactionExpired:
                                throw new TransactionExpiredException("Transaction Expired", ex.Cause, result);

                            // Raise TransactionCommitAmbiguous to application, with a cause of err.cause.
                            case TransactionOperationFailedException.FinalError.TransactionCommitAmbiguous:
                                throw new TransactionCommitAmbiguousException("Transaction may have failed to commit.", ex.Cause, result);

                            default:
                                throw new TransactionFailedException("Transaction failed.", ex.Cause ?? ex, result);
                        }
                    }
                    catch (Exception notWrapped)
                    {
                        if (singleQueryTransactionMode)
                        {
                            if (notWrapped is TransactionExpiredException)
                            {
                                throw new Core.Exceptions.UnambiguousTimeoutException("Timed out during single query transaction (L2)", notWrapped);
                            }

                            throw;
                        }

                        // Assert err is an ErrorWrapper
                        throw new InvalidOperationException(
                            $"All exceptions should have been wrapped in an {nameof(TransactionOperationFailedException)}.",
                            notWrapped);
                    }
                }
                catch (TransactionExpiredException err) when (singleQueryTransactionMode)
                {
                    throw new Core.Exceptions.UnambiguousTimeoutException("Timed out during single query transaction (L3)", err);
                }
            }
        }

        /// <summary>
        /// Run a single query as a transaction.
        /// </summary>
        /// <typeparam name="T">The type of the result.  Use <see cref="object"/> for queries with no results.</typeparam>
        /// <param name="statement">The statement to execute.</param>
        /// <param name="config">The configuration to use for this transaction.</param>
        /// <param name="scope">The scope</param>
        /// <returns>A <see cref="SingleQueryTransactionResult{T}"/> with the query results, if any.</returns>
        public async Task<SingleQueryTransactionResult<T>> QueryAsync<T>(string statement, SingleQueryTransactionConfigBuilder? config = null, IScope? scope = null)
        {
            using var rootSpan = _requestTracer.RequestSpan(nameof(QueryAsync))
                .SetAttribute("db.couchbase.transactions.tximplicit", true);

            config ??= SingleQueryTransactionConfigBuilder.Create();
            var options = config.QueryOptionsValue ?? new();
            var perTransactionConfig = config.Build();

            var txImplicit = true;
            IQueryResult<T>? queryResult = null;
            var transactionResult = await RunAsync(async ctx =>
            {
                var originalQueryResult = await ctx.QueryAsync<T>(statement, options, txImplicit, scope, rootSpan).CAF();
                queryResult = new Internal.SingleQueryResultWrapper<T>(originalQueryResult, ctx);
            }, perTransactionConfig, singleQueryTransactionMode: true).CAF();

            return new SingleQueryTransactionResult<T>()
            {
                Logs = transactionResult.Logs,
                QueryResult = queryResult,
                UnstagingComplete = transactionResult.UnstagingComplete
            };
        }

        private async Task ExecuteApplicationLambda(Func<AttemptContext, Task> transactionLogic,
                                                    TransactionContext overallContext,
                                                    ILoggerFactory loggerFactory,
                                                    TransactionResult result,
                                                    IRequestSpan parentSpan,
                                                    bool singleQueryTransactionMode)
        {
            var attemptid = Guid.NewGuid().ToString();
            using var traceSpan = parentSpan.ChildSpan(nameof(ExecuteApplicationLambda))
                .SetAttribute(Support.TransactionFields.AttemptId, attemptid);
            var delegatingLoggerFactory = new LogUtil.TransactionsLoggerFactory(loggerFactory, overallContext);
            var memoryLogger = delegatingLoggerFactory.CreateLogger(nameof(ExecuteApplicationLambda));
            var ctx = new AttemptContext(
                overallContext,
                attemptid,
                TestHooks,
                _redactor,
                delegatingLoggerFactory,
                _cluster,
                DocumentRepository,
                AtrRepository,
                requestTracer: _requestTracer
            );

            try
            {
                try
                {
                    await transactionLogic(ctx).CAF();
                    if (!singleQueryTransactionMode)
                    {
                        await ctx.AutoCommit(traceSpan).CAF();
                    }
                }
                catch (TransactionOperationFailedException)
                {
                    // already a classified error
                    throw;
                }
                catch (Exception innerEx)
                {
                    if (singleQueryTransactionMode)
                    {
                        throw;
                    }

                    // If err is not an ErrorWrapper, follow
                    // Exceptions Raised by the Application Lambda logic to turn it into one.
                    // From now on, all errors must be an ErrorWrapper.
                    // https://hackmd.io/foGjnSSIQmqfks2lXwNp8w?view#Exceptions-Raised-by-the-Application-Lambda
                    var error = ErrorBuilder.CreateError(ctx, innerEx.Classify()).Cause(innerEx);
                    if (innerEx is IRetryable)
                    {
                        error.RetryTransaction();
                    }

                    throw error.Build();
                }
            }
            catch (TransactionOperationFailedException ex)
            {
                // If err.rollback is true (it generally will be), auto-rollback the attempt by calling rollbackInternal with appRollback=false.
                if (ex.AutoRollbackAttempt && !singleQueryTransactionMode)
                {
                    try
                    {
                        memoryLogger.LogWarning("Attempt failed, attempting automatic rollback...");
                        await ctx.RollbackInternal(isAppRollback: false, parentSpan: traceSpan).CAF();
                    }
                    catch (Exception rollbackEx)
                    {
                        memoryLogger.LogWarning("Rollback failed due to {reason}", rollbackEx.Message);
                        // if rollback failed, raise the original error, but with retry disabled:
                        // Error(ec = err.ec, cause = err.cause, raise = err.raise
                        throw ErrorBuilder.CreateError(ctx, ex.CausingErrorClass)
                            .Cause(ex.Cause)
                            .DoNotRollbackAttempt()
                            .RaiseException(ex.FinalErrorToRaise)
                            .Build();
                    }
                }

                // If the transaction has expired, raised Error(ec = FAIL_EXPIRY, rollback=false, raise = TRANSACTION_EXPIRED)
                if (overallContext.IsExpired)
                {
                    if (ex.CausingErrorClass == ErrorClass.FailExpiry)
                    {
                        // already FailExpiry
                        throw;
                    }

                    memoryLogger.LogWarning("Transaction is expired.  No more retries or rollbacks.");
                    throw ErrorBuilder.CreateError(ctx, ErrorClass.FailExpiry)
                        .DoNotRollbackAttempt()
                        .RaiseException(TransactionOperationFailedException.FinalError.TransactionExpired)
                        .Build();
                }

                // Else if it succeeded or no rollback was performed, propagate err up.
                memoryLogger.LogDebug("Propagating error up. (ec = {ec}, retry = {retry}, finalError = {finalError})", ex.CausingErrorClass, ex.RetryTransaction, ex.FinalErrorToRaise);
                throw;
            }
            finally
            {
                result.UnstagingComplete = ctx.UnstagingComplete;
                memoryLogger.LogInformation("Attempt {attemptId} completed.  CleanupClient={cleanupClientAttempts}, LostCleanup={lostCleanup}", ctx.AttemptId, Config.CleanupConfig.CleanupClientAttempts, Config.CleanupConfig.CleanupLostAttempts);
                if (Config.CleanupConfig.CleanupClientAttempts)
                {
                    memoryLogger.LogInformation("Adding cleanup request for {attemptId}", ctx.AttemptId);
                    AddCleanupRequest(ctx);
                }

                result.Logs = overallContext.Logs;
            }
        }

        private void AddCleanupRequest(AttemptContext ctx)
        {
            var cleanupRequest = ctx.GetCleanupRequest();
            if (cleanupRequest != null)
            {
                if (!_cleanupWorkQueue.TryAddCleanupRequest(cleanupRequest))
                {
                    _logger.LogWarning("Failed to add background cleanup request: {req}", cleanupRequest);
                }
            }
        }

        internal async IAsyncEnumerable<TransactionCleanupAttempt> CleanupAttempts()
        {
            foreach (var cleanupRequest in _cleanupWorkQueue.RemainingCleanupRequests)
            {
                yield return await _cleaner.ProcessCleanupRequest(cleanupRequest).CAF();
            }
        }

        private void DisposeCommon()
        {
            _cleanupWorkQueue.Dispose();
        }

        public void Dispose()
        {
            lock (_syncObject)
            {
                if (_disposed) return;
                _disposed = true;
            }

            DisposeCommon();
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            lock (_syncObject)
            {
                if (_disposed) return;
                _disposed = true;
            }

            _logger.LogInformation("Transactions DisposeAsync called...");
            if (Config.CleanupConfig.CleanupClientAttempts)
            {
                _ = await CleanupAttempts().ToListAsync().CAF();
                _logger.LogInformation("Cleanup Client attempt disposed.");
            }

            if (_lostTransactionsCleanup != null)
            {
                await _lostTransactionsCleanup.DisposeAsync().CAF();
                _logger.LogInformation("Lost Transaction Cleanup disposed.");
            }
            DisposeCommon();
        }
        internal void AddCollectionToCleanup(ICouchbaseCollection couchbaseCollection)
        {
            // just call AddCollection if there is a lost transaction manager...
            if (_lostTransactionsCleanup is LostTransactionManager ltm)
            {
                ltm.AddCollection(couchbaseCollection);
            }
        }
    }
}




/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
