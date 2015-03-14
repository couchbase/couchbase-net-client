using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Configuration;
using Couchbase.Core.Diagnostics;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.N1QL;
using Couchbase.Views;

namespace Couchbase.Core.Buckets
{
    /// <summary>
    /// An implementation of <see cref="IRequestExecuter"/> for executing Couchbase bucket operations (Memcached, Views, N1QL, etc)
    /// against a persistent, Couchbase Bucket on a Couchbase cluster.
    /// </summary>
    internal class CouchbaseRequestExecuter : IRequestExecuter
    {
        private const uint OperationLifeSpan = 2500;
        private static readonly ILog Log = LogManager.GetLogger<CouchbaseRequestExecuter>();
        private readonly string _bucketName;
        private readonly IClusterController _clusterController;
        private readonly IConfigInfo _configInfo;
        private readonly IByteConverter _converter;
        private readonly Func<TimingLevel, object, IOperationTimer> Timer;
        private volatile bool _timingEnabled;

        public CouchbaseRequestExecuter(IClusterController clusterController, IConfigInfo configInfo, IByteConverter converter, string bucketName)
        {
            _clusterController = clusterController;
            _configInfo = configInfo;
            _converter = converter;
            _bucketName = bucketName;
            Timer = _clusterController.Configuration.Timer;
            _timingEnabled = _clusterController.Configuration.EnableOperationTiming;
        }

        public bool CanRetryOperation<T>(IOperationResult<T> operationResult, IOperation<T> operation)
        {
            var responseStatus = operationResult.Status;
            if (responseStatus == ResponseStatus.VBucketBelongsToAnotherServer)
            {
                return CheckForConfigUpdates(operation);
            }
            return IsResponseRetryable(responseStatus) && OperationSupportsRetries(operation);
        }

        public bool CheckForConfigUpdates(IOperation operation)
        {
            var requiresRetry = false;
            try
            {
                var bucketConfig = operation.GetConfig();
                if (bucketConfig != null)
                {
                    Log.Info(m => m("New config found {0}|{1}", bucketConfig.Rev, _configInfo.BucketConfig.Rev));
                    _clusterController.NotifyConfigPublished(bucketConfig);
                    requiresRetry = true;
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            return requiresRetry;
        }

        /// <summary>
        /// Gets the <see cref="Server"/> or node that a key has been mapped to.
        /// </summary>
        /// <param name="key">The key to get or set.</param>
        /// <param name="vBucket">The VBucket the key belongs to.</param>
        /// <returns>The <see cref="IServer"/> that the key is mapped to.</returns>
        public IServer GetServer(string key, out IVBucket vBucket)
        {
            var keyMapper = _configInfo.GetKeyMapper();
            vBucket = (IVBucket)keyMapper.MapKey(key);
            return vBucket.LocatePrimary();
        }

        public bool HandleIOError<T>(IOperation<T> operation, IServer server)
        {
            var retry = false;
            var exception = operation.Exception as IOException;
            if (exception != null)
            {
                try
                {
                    //Mark the current server as dead and force a reconfig
                    server.MarkDead();

                    var liveServer = _configInfo.GetServer();
                    var result = liveServer.Send(new Config(_converter, liveServer.EndPoint, OperationLifeSpan));
                    Log.Info(m => m("Trying to reconfig with {0}: {1}", liveServer.EndPoint, result.Message));
                    if (result.Success)
                    {
                        var config = result.Value;
                        if (config != null)
                        {
                            _clusterController.NotifyConfigPublished(result.Value);
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Info(e);
                }
            }
            return retry;
        }

        public bool IsResponseRetryable(ResponseStatus responseStatus)
        {
            var retryResponse = false;
            switch (responseStatus)
            {
                //client responses
                case ResponseStatus.ClientFailure:
                case ResponseStatus.OperationTimeout:
                    break;

                //server responses
                case ResponseStatus.Success:
                case ResponseStatus.KeyNotFound:
                case ResponseStatus.KeyExists:
                case ResponseStatus.ValueTooLarge:
                case ResponseStatus.InvalidArguments:
                case ResponseStatus.ItemNotStored:
                case ResponseStatus.IncrDecrOnNonNumericValue:
                case ResponseStatus.AuthenticationError:
                case ResponseStatus.AuthenticationContinue:
                case ResponseStatus.InvalidRange:
                case ResponseStatus.UnknownCommand:
                case ResponseStatus.OutOfMemory:
                case ResponseStatus.NotSupported:
                case ResponseStatus.InternalError:
                case ResponseStatus.Busy:
                case ResponseStatus.TemporaryFailure:
                    break;
                case ResponseStatus.VBucketBelongsToAnotherServer:
                    retryResponse = true;
                    break;
            }
            return retryResponse;
        }

        public bool OperationSupportsRetries<T>(IOperation<T> operation)
        {
            var supportsRetry = false;
            switch (operation.OperationCode)
            {
                case OperationCode.Get:
                case OperationCode.Add:
                    supportsRetry = true;
                    break;
                case OperationCode.Set:
                case OperationCode.Replace:
                case OperationCode.Delete:
                case OperationCode.Append:
                case OperationCode.Prepend:
                    supportsRetry = operation.Cas > 0;
                    break;
                case OperationCode.Increment:
                case OperationCode.Decrement:
                case OperationCode.GAT:
                case OperationCode.Touch:
                    break;
            }
            Log.Debug(m=>m("{0} supports retries: {1}", operation.OperationCode, supportsRetry));
            return supportsRetry;
        }

        static async Task<IViewResult<T>> RetryViewEvery<T>(Func<IViewQuery, IConfigInfo, Task<IViewResult<T>>> execute, IViewQuery query, IConfigInfo configInfo, CancellationToken cancellationToken)
        {
            while (true)
            {
                var result = await execute(query, configInfo);
                if (query.RetryAttempts++ >= configInfo.ClientConfig.MaxViewRetries ||
                    result.Success ||
                    result.CannotRetry())
                {
                    return result;
                }
                Log.Debug(m => m("trying again: {0}", query.RetryAttempts));
                var sleepTime = (int)Math.Pow(2, query.RetryAttempts);
                var task = Task.Delay(sleepTime, cancellationToken);
                try
                {
                    await task;
                }
                catch (TaskCanceledException)
                {
                    return result;
                }
            }
        }

        /// <summary>
        /// Sends an operation to the server while observing it's durability requirements
        /// </summary>
        /// <typeparam name="T">The value for T.</typeparam>
        /// <param name="operation">A binary memcached operation - must be a mutation operation.</param>
        /// <param name="deletion">True if mutation is a deletion.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="IOperationResult{T}"/> with it's <see cref="Durability"/> status.</returns>
        public IOperationResult<T> SendWithDurability<T>(IOperation<T> operation, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            var result = SendWithRetry(operation);
            if (result.Success)
            {
                var config = _configInfo.ClientConfig.BucketConfigs[_bucketName];
                var observer = new KeyObserver(_configInfo, config.ObserveInterval, config.ObserveTimeout);
                var observed = observer.Observe(operation.Key, result.Cas, deletion, replicateTo, persistTo);
                result.Durability = observed
                    ? Durability.Satisfied
                    : Durability.NotSatisfied;
            }
            else
            {
                result.Durability = Durability.NotSatisfied;
            }
            return result;
        }

        /// <summary>
        /// Sends an operation to the server while observing it's durability requirements using async/await
        /// </summary>
        /// <typeparam name="T">The value for T.</typeparam>
        /// <param name="operation">A binary memcached operation - must be a mutation operation.</param>
        /// <param name="deletion">True if mutation is a deletion.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> to be awaited on with it's <see cref="Durability"/> status.</returns>
        public Task<IOperationResult<T>> SendWithDurabilityAsync<T>(IOperation<T> operation, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a View request with retry.
        /// </summary>
        /// <typeparam name="T">The Type T of the <see cref="ViewRow{T}"/> value.</typeparam>
        /// <param name="viewQuery">The view query.</param>
        /// <returns>A <see cref="IViewResult{T}"/> with the results of the query.</returns>
        public IViewResult<T> SendWithRetry<T>(IViewQuery viewQuery)
        {
            IViewResult<T> viewResult = null;
            try
            {
                do
                {
                    var server = _configInfo.GetServer();
                    viewResult = server.Send<T>(viewQuery);
                } while (
                    !viewResult.Success &&
                    !viewResult.CannotRetry() &&
                    viewQuery.RetryAttempts++ <= _configInfo.ClientConfig.MaxViewRetries);
            }
            catch (Exception e)
            {
                Log.Info(e);
                const string message = "View request failed, check Error and Exception fields for details.";
                viewResult = new ViewResult<T>
                {
                    Message = message,
                    Error = e.Message,
                    StatusCode = HttpStatusCode.BadRequest,
                    Success = false,
                    Exception = e
                };
            }
            return viewResult;
        }

        /// <summary>
        /// Sends a <see cref="IOperation{T}" /> to the Couchbase Server using the Memcached protocol.
        /// </summary>
        /// <typeparam name="T">The Type of the body of the request.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}" /> to send.</param>
        /// <returns>
        /// An <see cref="IOperationResult" /> with the status of the request.
        /// </returns>
        public IOperationResult<T> SendWithRetry<T>(IOperation<T> operation)
        {
            if (Log.IsDebugEnabled && _timingEnabled)
            {
                operation.Timer = Timer;
                operation.BeginTimer(TimingLevel.Three);
            }

            IOperationResult<T> operationResult = new OperationResult<T> { Success = false };
            do
            {
                IVBucket vBucket;
                var server = GetServer(operation.Key, out vBucket);
                if (server == null)
                {
                    continue;
                }
                operation.VBucket = vBucket;
                operationResult = server.Send(operation);

                if (operationResult.Success)
                {
                    Log.Debug(
                        m =>
                            m("Operation {0} succeeded {1} for key {2} : {3}", operation.GetType().Name,
                                operation.Attempts, operation.Key, operationResult.Value));
                    break;
                }
                if (CanRetryOperation(operationResult, operation) && !operation.TimedOut())
                {
                    IOperation<T> operation1 = operation;
                    IOperationResult<T> result = operationResult;
                    Log.Debug(m => m("Operation retry {0} for key {1} using vb{2} from rev{3} and opaque{4}. Reason: {5}",
                        operation1.Attempts, operation1.Key, operation1.VBucket.Index, operation1.VBucket.Rev, operation1.Opaque, result.Message));

                    operation = operation.Clone();
                }
                else
                {
                    Log.Debug(m => m("Operation doesn't support retries for key {0}", operation.Key));
                    break;
                }
            } while (!operationResult.Success && !operation.TimedOut());

            if (!operationResult.Success)
            {
                if (operation.TimedOut())
                {
                    const string msg = "The operation has timed out.";
                    ((OperationResult)operationResult).Message = msg;
                    ((OperationResult)operationResult).Status = ResponseStatus.OperationTimeout;
                }

                const string msg1 = "Operation for key {0} failed after {1} retries using vb{2} from rev{3} and opaque{4}. Reason: {5}";
                Log.Debug(m => m(msg1, operation.Key, operation.Attempts, operation.VBucket.Index, operation.VBucket.Rev, operation.Opaque, operationResult.Message));
            }

            if (Log.IsDebugEnabled && _timingEnabled)
            {
                operation.EndTimer(TimingLevel.Three);
            }

            return operationResult;
        }

        /// <summary>
        /// Sends a View request to the server to be executed using async/await
        /// </summary>
        /// <typeparam name="T">The Type of the body of the Views return value or row.</typeparam>
        /// <param name="query">An <see cref="IViewQuery" /> to be executed.</param>
        /// <returns>
        /// The result of the View request as an <see cref="Task{IViewResult}" /> to be awaited on where T is the Type of each row.
        /// </returns>
        public async Task<IViewResult<T>> SendWithRetryAsync<T>(IViewQuery query)
        {
            IViewResult<T> viewResult = null;
            try
            {
                using (var cancellationTokenSource = new CancellationTokenSource(_configInfo.ClientConfig.ViewHardTimeout))
                {
                    var task = RetryViewEvery(async (e, c) =>
                    {
                        var server = c.GetServer();
                        return await server.SendAsync<T>(query);
                    },
                    query, _configInfo, cancellationTokenSource.Token);
                    task.ConfigureAwait(false);
                    viewResult = await task;
                }
            }
            catch (Exception e)
            {
                Log.Info(e);
                const string message = "View request failed, check Error and Exception fields for details.";
                viewResult = new ViewResult<T>
                {
                    Message = message,
                    Error = e.Message,
                    StatusCode = HttpStatusCode.BadRequest,
                    Success = false,
                    Exception = e
                };
            }
            return viewResult;
        }

        /// <summary>
        /// Sends a <see cref="IOperation{T}" /> to the Couchbase Server using the Memcached protocol using async/await.
        /// </summary>
        /// <typeparam name="T">The Type of the body of the request.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}" /> to send.</param>
        /// <returns>
        /// An <see cref="Task{IOperationResult}" /> with the status of the request to be awaited on.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public Task<IOperationResult<T>> SendWithRetryAsync<T>(IOperation<T> operation)
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
        public IQueryResult<T> SendWithRetry<T>(IQueryRequest queryRequest)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sends a N1QL query to the server to be executed using the <see cref="IQueryRequest"/> object using async/await.
        /// </summary>
        /// <typeparam name="T">The Type T of the body of each result row.</typeparam>
        /// <param name="queryRequest">The <see cref="IQueryRequest"/> object to send to the server.</param>
        /// <returns>An <see cref="Task{IQueryResult}"/> object to be awaited on that is the result of the query.</returns>
        public Task<IQueryResult<T>> SendWithRetryAsync<T>(IQueryRequest queryRequest)
        {
            throw new NotImplementedException();
        }
    }
}
