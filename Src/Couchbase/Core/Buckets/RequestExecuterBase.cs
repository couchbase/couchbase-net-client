using Common.Logging;
using Couchbase.Configuration;
using Couchbase.Core.Diagnostics;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.N1QL;
using Couchbase.Views;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Services;
using Couchbase.Search;

namespace Couchbase.Core.Buckets
{
    /// <summary>
    /// Provides virtual methods and base implementations for <see cref="IRequestExecuter"/>.
    /// </summary>
    internal abstract class RequestExecuterBase : IRequestExecuter
    {
        protected readonly int OperationLifeSpan;
        protected static readonly ILog Log = LogManager.GetLogger<RequestExecuterBase>();
        protected readonly string BucketName;
        protected readonly IClusterController ClusterController;
        protected readonly Func<TimingLevel, object, IOperationTimer> Timer;
        protected volatile bool TimingEnabled;
        protected readonly ConcurrentDictionary<uint, IOperation> Pending;

        protected RequestExecuterBase(IClusterController clusterController, IConfigInfo configInfo, string bucketName, ConcurrentDictionary<uint, IOperation> pending)
        {
            ClusterController = clusterController;
            TimingEnabled = ClusterController.Configuration.EnableOperationTiming;
            BucketName = bucketName;
            OperationLifeSpan = (int)ClusterController.Configuration.DefaultOperationLifespan;
            Pending = pending;
            Timer = ClusterController.Configuration.Timer;
            ConfigInfo = configInfo;
        }

        public IConfigInfo ConfigInfo { get; private set; }

        /// <summary>
        /// Executes an operation until it either succeeds, reaches a non-retriable state, or times out.
        /// </summary>
        /// <typeparam name="T">The Type of the <see cref="IOperation"/>'s value.</typeparam>
        /// <param name="execute">A delegate that contains the send logic.</param>
        /// <param name="operation">The <see cref="IOperation"/> to execiute.</param>
        /// <param name="configInfo">The <see cref="IConfigInfo"/> that represents the logical topology of the cluster.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> for timing out the request.</param>
        /// An <see cref="Task{IOperationResult}"/> object representing the asynchrobous operation.
        public virtual async Task<IOperationResult<T>> RetryOperationEveryAsync<T>(
            Func<IOperation<T>, IConfigInfo, Task<IOperationResult<T>>> execute,
            IOperation<T> operation,
            IConfigInfo configInfo,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                var result = await execute(operation, configInfo).ConfigureAwait(false);
                if (result.Success || operation.TimedOut())
                {
                    if (operation.TimedOut())
                    {
                        const string msg = "The operation has timed out. Retried [{0}] times.";
                        ((OperationResult)result).Message = string.Format(msg, operation.Attempts);
                        ((OperationResult)result).Status = ResponseStatus.OperationTimeout;
                    }
                    return result;
                }
                if (!result.ShouldRetry() && !result.IsNmv() && !operation.CanRetry())
                {
                    return result;
                }

                operation.Attempts++;
                var sleepTime = (int)Math.Pow(2, operation.Attempts * 2);
                var task = Task.Delay(sleepTime, cancellationToken).ConfigureAwait(false);
                try
                {
                    await task;
                }
                catch (TaskCanceledException)
                {
                    const string msg = "The operation has timed out. Retried [{0}] times.";
                    ((OperationResult)result).Message = string.Format(msg, operation.Attempts);
                    ((OperationResult)result).Status = ResponseStatus.OperationTimeout;
                    return result;
                }
            }
        }

        /// <summary>
        /// Executes an operation until it either succeeds, reaches a non-retriable state, or times out.
        /// </summary
        /// <param name="execute">A delegate that contains the send logic.</param>
        /// <param name="operation">The <see cref="IOperation"/> to execiute.</param>
        /// <param name="configInfo">The <see cref="IConfigInfo"/> that represents the logical topology of the cluster.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> for timing out the request.</param>
        /// An <see cref="Task{IOperationResult}" /> object representing the asynchrobous operation.
        public virtual async Task<IOperationResult> RetryOperationEveryAsync(
            Func<IOperation, IConfigInfo, Task<IOperationResult>> execute,
            IOperation operation,
            IConfigInfo configInfo,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                var result = await execute(operation, configInfo).ConfigureAwait(false);
                if (result.Success || operation.TimedOut())
                {
                    if (operation.TimedOut())
                    {
                        const string msg = "The operation has timed out. Retried [{0}] times.";
                        ((OperationResult)result).Message = string.Format(msg, operation.Attempts);
                        ((OperationResult)result).Status = ResponseStatus.OperationTimeout;
                    }
                    return result;
                }
                if (!result.IsNmv() || !operation.CanRetry())
                {
                    return result;
                }
                operation.Attempts++;
                var sleepTime = (int)Math.Pow(2, operation.Attempts * 2);
                var task = Task.Delay(sleepTime, cancellationToken).ConfigureAwait(false);
                try
                {
                    await task;
                }
                catch (TaskCanceledException)
                {
                    const string msg = "The operation has timed out. Retried [{0}] times.";
                    ((OperationResult)result).Message = string.Format(msg, operation.Attempts);
                    ((OperationResult)result).Status = ResponseStatus.OperationTimeout;
                    return result;
                }
            }
        }

        /// <summary>
        /// Sends a <see cref="IOperation{T}" /> to the Couchbase Server using the Memcached protocol.
        /// </summary>
        /// <typeparam name="T">The Type of the body of the request.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}" /> to send.</param>
        /// <returns>
        /// An <see cref="IOperationResult" /> with the status of the request.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual IOperationResult<T> SendWithRetry<T>(IOperation<T> operation)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a <see cref="IOperation{T}" /> to the Couchbase Server using the Memcached protocol using async/await.
        /// </summary>
        /// <typeparam name="T">The Type of the body of the request.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}" /> to send.</param>
        /// <param name="tcs"></param>
        /// <param name="cts"></param>
        /// <returns>
        /// An <see cref="Task{IOperationResult}" /> with the status of the request to be awaited on.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual Task<IOperationResult<T>> SendWithRetryAsync<T>(IOperation<T> operation,
            TaskCompletionSource<IOperationResult<T>> tcs = null,
            CancellationTokenSource cts = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a View request to the server to be executed.
        /// </summary>
        /// <typeparam name="T">The Type of the body of the Views return value or row.</typeparam>
        /// <param name="query">An <see cref="IViewQuery" /> to be executed.</param>
        /// <returns>
        /// The result of the View request as an <see cref="IViewResult{T}" /> where T is the Type of each row.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual IViewResult<T> SendWithRetry<T>(IViewQueryable query)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a View request to the server to be executed using async/await
        /// </summary>
        /// <typeparam name="T">The Type of the body of the Views return value or row.</typeparam>
        /// <param name="query">An <see cref="IViewQuery" /> to be executed.</param>
        /// <returns>
        /// The result of the View request as an <see cref="Task{IViewResult}" /> to be awaited on where T is the Type of each row.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual Task<IViewResult<T>> SendWithRetryAsync<T>(IViewQueryable query)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a N1QL query to the server to be executed using the <see cref="IQueryRequest" /> object.
        /// </summary>
        /// <typeparam name="T">The Type T of the body of each result row.</typeparam>
        /// <param name="queryRequest">The <see cref="IQueryRequest" /> object to send to the server.</param>
        /// <returns>
        /// An <see cref="IQueryResult{T}" /> object that is the result of the query.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual N1QL.IQueryResult<T> SendWithRetry<T>(N1QL.IQueryRequest queryRequest)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a N1QL query to the server to be executed using the <see cref="IQueryRequest" /> object using async/await.
        /// </summary>
        /// <typeparam name="T">The Type T of the body of each result row.</typeparam>
        /// <param name="queryRequest">The <see cref="IQueryRequest" /> object to send to the server.</param>
        /// <returns>
        /// An <see cref="Task{IQueryResult}" /> object to be awaited on that is the result of the query.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual Task<N1QL.IQueryResult<T>> SendWithRetryAsync<T>(N1QL.IQueryRequest queryRequest)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a <see cref="IFtsQuery" /> request to an FTS enabled node and returns the <see cref="ISearchQueryResult" />response.
        /// </summary>
        /// <returns>
        /// A <see cref="ISearchQueryResult" /> representing the response from the FTS service.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual ISearchQueryResult SendWithRetry(SearchQuery searchQuery)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a <see cref="IFtsQuery" /> request to an FTS enabled node and returns the <see cref="ISearchQueryResult" />response.
        /// </summary>
        /// <param name="searchQuery"></param>
        /// <returns>
        /// A <see cref="Task{ISearchQueryResult}" /> representing the response from the FTS service.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual Task<ISearchQueryResult> SendWithRetryAsync(SearchQuery searchQuery)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends an operation to the server while observing it's durability requirements
        /// </summary>
        /// <typeparam name="T">The value for T.</typeparam>
        /// <param name="operation">A binary memcached operation - must be a mutation operation.</param>
        /// <param name="deletion">True if mutation is a deletion.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="IOperationResult{T}" /> with it's <see cref="Durability" /> status.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual IOperationResult<T> SendWithDurability<T>(IOperation<T> operation, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends an operation to the server while observing it's durability requirements using async/await
        /// </summary>
        /// <typeparam name="T">The value for T.</typeparam>
        /// <param name="operation">A binary memcached operation - must be a mutation operation.</param>
        /// <param name="deletion">True if mutation is a deletion.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> to be awaited on with it's <see cref="Durability" /> status.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual Task<IOperationResult<T>> SendWithDurabilityAsync<T>(IOperation<T> operation, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a <see cref="IOperation" /> to the Couchbase Server using the Memcached protocol.
        /// </summary>
        /// <param name="operation">The <see cref="IOperation" /> to send.</param>
        /// <returns>
        /// An <see cref="IOperationResult" /> with the status of the request.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual IOperationResult SendWithRetry(IOperation operation)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a <see cref="IOperation" /> to the Couchbase Server using the Memcached protocol using async/await.
        /// </summary>
        /// <param name="operation">The <see cref="IOperation" /> to send.</param>
        /// <returns>
        /// An <see cref="Task{IOperationResult}" /> with the status of the request to be awaited on.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual Task<IOperationResult> SendWithRetryAsync(IOperation operation,
            TaskCompletionSource<IOperationResult> tcs = null,
            CancellationTokenSource cts = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends an operation to the server while observing it's durability requirements
        /// </summary>
        /// <param name="operation">A binary memcached operation - must be a mutation operation.</param>
        /// <param name="deletion">True if mutation is a deletion.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="IOperationResult" /> with it's <see cref="Durability" /> status.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual IOperationResult SendWithDurability(IOperation operation, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends an operation to the server while observing it's durability requirements using async/await
        /// </summary>
        /// <param name="operation">A binary memcached operation - must be a mutation operation.</param>
        /// <param name="deletion">True if mutation is a deletion.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> to be awaited on with it's <see cref="Durability" /> status.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public virtual Task<IOperationResult> SendWithDurabilityAsync(IOperation operation, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Checks the primary node for the key, if a NMV is encountered, will retry on each replica.
        /// </summary>
        /// <typeparam name="T">The Type of the body of the request.</typeparam>
        /// <param name="operation">The <see cref="IOperation" /> to execiute.</param>
        /// <returns>
        /// The result of the operation.
        /// </returns>
        public IOperationResult<T> ReadFromReplica<T>(ReplicaRead<T> operation)
        {
            //Is the cluster configured for Data services?
            if (!ConfigInfo.IsDataCapable)
                throw new ServiceNotSupportedException("The cluster does not support Data services.");

            IOperationResult<T> result = new OperationResult<T> {Success = false};
            do
            {
                var keyMapper = ConfigInfo.GetKeyMapper();
                var vBucket = (IVBucket) keyMapper.MapKey(operation.Key);
                operation.VBucket = vBucket;

                if (vBucket.HasReplicas)
                {
                    foreach (var index in vBucket.Replicas)
                    {
                        var replica = vBucket.LocateReplica(index);
                        if (replica == null) continue;
                        result = replica.Send(operation);
                        if (result.Success && !result.IsNmv())
                        {
                            return result;
                        }
                        operation = (ReplicaRead<T>) operation.Clone();
                    }
                }
                else
                {
                    result = new OperationResult<T>
                    {
                        Status = ResponseStatus.NoReplicasFound,
                        Message = "No replicas found; have you configured the bucket for replica reads?",
                        Success = false
                    };
                }
            } while (result.ShouldRetry() && !result.Success && !operation.TimedOut());

            if (!result.Success)
            {
                if (operation.TimedOut() &&
                    (result.Status != ResponseStatus.NoReplicasFound && result.Status != ResponseStatus.KeyNotFound))
                {
                    const string msg = "The operation has timed out.";
                    ((OperationResult)result).Message = msg;
                    ((OperationResult)result).Status = ResponseStatus.OperationTimeout;
                }
                LogFailure(operation, result);
            }
            return result;
        }

        /// <summary>
        /// Checks the primary node for the key, if a NMV is encountered, will retry on each replica, asynchronously.
        /// </summary>
        /// <typeparam name="T">The Type of the body of the request.</typeparam>
        /// <param name="operation">The <see cref="IOperation" /> to execiute.</param>
        /// <returns>
        /// The <see cref="Task{IOperationResult}" /> object representing asynchcronous operation.
        /// </returns>
        public async Task<IOperationResult<T>> ReadFromReplicaAsync<T>(ReplicaRead<T> operation)
        {
            var tcs = new TaskCompletionSource<IOperationResult<T>>();
            var cts = new CancellationTokenSource(OperationLifeSpan);
            cts.CancelAfter(OperationLifeSpan);

            var keyMapper = ConfigInfo.GetKeyMapper();
            var vBucket = (IVBucket)keyMapper.MapKey(operation.Key);

            operation.VBucket = vBucket;
            operation.Completed = CallbackFactory.CompletedFuncForRetry(this, Pending, ClusterController, tcs);
            Pending.TryAdd(operation.Opaque, operation);

           IOperationResult<T> result = null;
            if (vBucket.HasReplicas)
            {
                foreach (var index in vBucket.Replicas)
                {
                    var replica = vBucket.LocateReplica(index);
                    if (replica == null) continue;

                    await replica.SendAsync(operation).ConfigureAwait(false);
                    result = await tcs.Task;
                    if (result.Success && !result.IsNmv())
                    {
                        return result;
                    }
                }
            }
            else
            {
                result = new OperationResult<T>
                {
                    Status = ResponseStatus.NoReplicasFound,
                    Message = "No replicas found; have you configured the bucket for replica reads?",
                    Success = false
                };
            }
            return result;
        }

        public IOperationResult<ObserveState> Observe(Observe operation)
        {
            var keyMapper = ConfigInfo.GetKeyMapper();
            var vBucket = (IVBucket)keyMapper.MapKey(operation.Key);
            operation.VBucket = vBucket;

            var server = vBucket.LocatePrimary();
            return server.Send(operation);
        }

        public Task<IOperationResult<ObserveState>> ObserveAsync(Observe operation)
        {
            var tcs = new TaskCompletionSource<IOperationResult<ObserveState>>();
            var cts = new CancellationTokenSource(OperationLifeSpan);
            cts.CancelAfter(OperationLifeSpan);

            var keyMapper = ConfigInfo.GetKeyMapper();
            var vBucket = (IVBucket)keyMapper.MapKey(operation.Key);

            operation.VBucket = vBucket;
            operation.Completed = CallbackFactory.CompletedFuncForRetry(this, Pending, ClusterController, tcs);
            Pending.TryAdd(operation.Opaque, operation);

            var server = vBucket.LocatePrimary();
            server.SendAsync(operation);

            return tcs.Task;
        }

        public void LogFailure(IOperation operation, IOperationResult operationResult)
        {
            var vBucket = operation.VBucket;
            if (vBucket != null)
            {
                const string msg1 = "Operation for key {0} failed after {1} retries using vb{2} from rev{3} and opaque{4}. Reason: {5}";
                Log.Debug(m => m(msg1, operation.Key, operation.Attempts, vBucket.Index, vBucket.Rev, operation.Opaque, operationResult.Message));
            }
            else
            {
                const string msg1 = "Operation for key {0} failed after {1} retries and opaque{2}. Reason: {3}";
                Log.Debug(m => m(msg1, operation.Key, operation.Attempts, operation.Opaque, operationResult.Message));
            }
        }

        public void UpdateConfig()
        {
            var node = ConfigInfo.GetDataNode();
            if (node != null && !node.IsDown)
            {
                Log.InfoFormat("Updating config on {0} using rev#{1}", node.EndPoint, node.Revision);
                var result = node.Send(new Config(ClusterController.Transcoder,
                    ConfigInfo.ClientConfig.DefaultOperationLifespan,
                    node.EndPoint));

                if (result.Success)
                {
                    ClusterController.NotifyConfigPublished(result.Value);
                }
            }
        }
    }
}