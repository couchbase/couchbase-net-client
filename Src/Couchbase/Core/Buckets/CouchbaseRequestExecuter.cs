using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Schema;
using Common.Logging;
using Couchbase.Configuration;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Diagnostics;
using Couchbase.Core.Services;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.N1QL;
using Couchbase.Search;
using Couchbase.Utils;
using Couchbase.Views;
using Newtonsoft.Json;

namespace Couchbase.Core.Buckets
{
    /// <summary>
    /// An implementation of <see cref="IRequestExecuter"/> for executing Couchbase bucket operations (Memcached, Views, N1QL, etc)
    /// against a persistent, Couchbase Bucket on a Couchbase cluster.
    /// </summary>
    internal class CouchbaseRequestExecuter : RequestExecuterBase
    {
        protected static readonly new ILog Log = LogManager.GetLogger<CouchbaseRequestExecuter>();
        private object _syncObj = new object();

        public CouchbaseRequestExecuter(IClusterController clusterController, IConfigInfo configInfo,
            string bucketName, ConcurrentDictionary<uint, IOperation> pending)
            : base(clusterController, configInfo, bucketName, pending)
        {
        }

        /// <summary>
        /// Checks the <see cref="IOperation"/> to see if it supports retries and then checks the <see cref="IOperationResult"/>
        ///  to see if the error or server response supports retries.
        /// </summary>
        /// <typeparam name="T">The Type of the body of the request.</typeparam>
        /// <param name="operationResult">The <see cref="IOperationResult"/> to check from the server.</param>
        /// <param name="operation">The <see cref="IOperation"/> to check to see if it supports retries. Not all operations support retries.</param>
        /// <returns></returns>
        public bool CanRetryOperation(IOperationResult operationResult, IOperation operation)
        {
            var responseStatus = operationResult.Status;
            if (responseStatus == ResponseStatus.VBucketBelongsToAnotherServer)
            {
                return CheckForConfigUpdates(operation);
            }
            return operation.CanRetry() && operationResult.ShouldRetry();
        }

        /// <summary>
        /// Updates the configuration if the <see cref="IOperation"/> returns a <see cref="IBucketConfig"/>
        /// </summary>
        /// <param name="operation">The <see cref="IOperation"/> with the <see cref="IBucketConfig"/> to check for.</param>
        /// <returns></returns>
        public bool CheckForConfigUpdates(IOperation operation)
        {
            var requiresRetry = false;
            try
            {
                var bucketConfig = operation.GetConfig();
                if (bucketConfig != null)
                {
                    Log.Info(m => m("New config found {0}|{1}", bucketConfig.Rev, ConfigInfo.BucketConfig.Rev));
                    Log.Debug(JsonConvert.SerializeObject(bucketConfig));
                    ClusterController.NotifyConfigPublished(bucketConfig);
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
        /// <param name="revision">The rev # of the cluster map.</param>
        /// <param name="vBucket">The VBucket the key belongs to.</param>
        /// <returns>The <see cref="IServer"/> that the key is mapped to.</returns>
        public IServer GetServer(string key, uint revision, out IVBucket vBucket)
        {
            var keyMapper = ConfigInfo.GetKeyMapper();
            vBucket = (IVBucket) keyMapper.MapKey(key, revision);
            return vBucket.LocatePrimary();
        }

        /// <summary>
        /// Executes an <see cref="IViewQuery"/> asynchronously. If it fails, the response is checked and
        ///  if certain criteria are met the request is retried until it times out.
        /// </summary>
        /// <typeparam name="T">The Type of View result body.</typeparam>
        /// <param name="execute">A delegate with the send logic that is executed on each attempt. </param>
        /// <param name="query">The <see cref="IViewQuery"/> to execute.</param>
        /// <param name="configInfo">The <see cref="IConfigInfo"/> that represents the logical topology of the cluster.</param>
        /// <param name="cancellationToken">For canceling the async operation.</param>
        /// <returns>A <see cref="Task{IViewResult}"/> object representing the asynchronous operation.</returns>
        static async Task<IViewResult<T>> RetryViewEveryAsync<T>(Func<IViewQueryable, IConfigInfo, Task<IViewResult<T>>> execute,
            IViewQueryable query,
            IConfigInfo configInfo,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                var result = await execute(query, configInfo).ContinueOnAnyContext();
                if (query.RetryAttempts++ >= configInfo.ClientConfig.MaxViewRetries ||
                    result.Success ||
                    result.CannotRetry())
                {
                    return result;
                }
                Log.Debug(m => m("trying again: {0}", query.RetryAttempts));
                var sleepTime = (int)Math.Pow(2, query.RetryAttempts);
                var task = Task.Delay(sleepTime, cancellationToken).ContinueOnAnyContext();
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

        static async Task<IQueryResult<T>> RetryQueryEveryAsync<T>(Func<IQueryRequest, IConfigInfo, Task<IQueryResult<T>>> execute,
            IQueryRequest query,
            IConfigInfo configInfo,
            CancellationToken cancellationToken)
        {
            const int maxAttempts = 10;
            var attempts = 0;
            while (true)
            {
                IResult result = await execute(query, configInfo).ContinueOnAnyContext();
                if (result.Success || query.TimedOut() ||
                    !result.ShouldRetry() || attempts >= maxAttempts)
                {
                    return (IQueryResult<T>)result;
                }

                Log.Debug(m => m("trying query again: {0}", attempts));
                var sleepTime = (int)Math.Pow(2, attempts++);
                var task = Task.Delay(sleepTime, cancellationToken).ContinueOnAnyContext();
                try
                {
                    await task;
                }
                catch (TaskCanceledException)
                {
                    return (IQueryResult<T>)result;
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
        /// <exception cref="ServiceNotSupportedException">The cluster does not support Data services.</exception>
        public override IOperationResult<T> SendWithDurability<T>(IOperation<T> operation, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            IOperationResult<T> result;
            try
            {
                //Is the cluster configured for Data services?
                if (!ConfigInfo.IsDataCapable)
                {
                    throw new ServiceNotSupportedException(
                        ExceptionUtil.GetMessage(ExceptionUtil.ServiceNotSupportedMsg, "Data"));
                }

                result = SendWithRetry(operation);
                if (result.Success)
                {
                    var config = ConfigInfo.ClientConfig.BucketConfigs[BucketName];

                    if (ConfigInfo.SupportsEnhancedDurability)
                    {
                        var seqnoObserver = new KeySeqnoObserver(ConfigInfo, ClusterController.Transcoder,
                            config.ObserveInterval, (uint) config.ObserveTimeout);

                        var observed = seqnoObserver.Observe(result.Token, replicateTo, persistTo);
                        result.Durability = observed ? Durability.Satisfied : Durability.NotSatisfied;
                    }
                    else
                    {
                        var observer = new KeyObserver(ConfigInfo, ClusterController.Transcoder,
                            config.ObserveInterval, config.ObserveTimeout);

                        var observed = observer.Observe(operation.Key, result.Cas, deletion, replicateTo, persistTo);
                        result.Durability = observed ? Durability.Satisfied : Durability.NotSatisfied;
                    }
                }
                else
                {
                    result.Durability = Durability.NotSatisfied;
                }
            }
            catch (ReplicaNotConfiguredException e)
            {
                result = new OperationResult<T>
                {
                    Exception = e,
                    Status = ResponseStatus.NoReplicasFound,
                    Durability = Durability.NotSatisfied
                };
            }
            catch (DocumentMutationLostException e)
            {
                result = new OperationResult<T>
                {
                    Exception = e,
                    Status = ResponseStatus.DocumentMutationLost,
                    Durability = Durability.NotSatisfied
                };
            }
            catch (Exception e)
            {
                result = new OperationResult<T>
                {
                    Exception = e,
                    Status = ResponseStatus.ClientFailure
                };
            }
            return result;
        }

        /// <summary>
        /// Sends an operation to the server while observing it's durability requirements
        /// </summary>
        /// <param name="operation">A binary memcached operation - must be a mutation operation.</param>
        /// <param name="deletion">True if mutation is a deletion.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="IOperationResult"/> with it's <see cref="Durability"/> status.</returns>
        /// <exception cref="ServiceNotSupportedException">The cluster does not support Data services.</exception>
        public override IOperationResult SendWithDurability(IOperation operation, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            IOperationResult result;
            try
            {
                //Is the cluster configured for Data services?
                if (!ConfigInfo.IsDataCapable)
                {
                    throw new ServiceNotSupportedException(
                        ExceptionUtil.GetMessage(ExceptionUtil.ServiceNotSupportedMsg, "Data"));
                }

                result = SendWithRetry(operation);
                if (result.Success)
                {
                    var config = ConfigInfo.ClientConfig.BucketConfigs[BucketName];

                    if (ConfigInfo.SupportsEnhancedDurability)
                    {
                        var seqnoObserver = new KeySeqnoObserver(ConfigInfo, ClusterController.Transcoder,
                            config.ObserveInterval, (uint) config.ObserveTimeout);

                        var observed = seqnoObserver.Observe(result.Token, replicateTo, persistTo);
                        result.Durability = observed ? Durability.Satisfied : Durability.NotSatisfied;
                    }
                    else
                    {
                        var observer = new KeyObserver(ConfigInfo, ClusterController.Transcoder,
                            config.ObserveInterval, config.ObserveTimeout);

                        var observed = observer.Observe(operation.Key, result.Cas, deletion, replicateTo, persistTo);
                        result.Durability = observed ? Durability.Satisfied : Durability.NotSatisfied;
                    }
                }
                else
                {
                    result.Durability = Durability.NotSatisfied;
                }
            }
            catch (ReplicaNotConfiguredException e)
            {
                result = new OperationResult
                {
                    Exception = e,
                    Status = ResponseStatus.NoReplicasFound,
                    Durability = Durability.NotSatisfied
                };
            }
            catch (DocumentMutationLostException e)
            {
                result = new OperationResult
                {
                    Exception = e,
                    Status = ResponseStatus.DocumentMutationLost,
                    Durability = Durability.NotSatisfied
                };
            }
            catch (Exception e)
            {
                result = new OperationResult
                {
                    Exception = e,
                    Status = ResponseStatus.ClientFailure
                };
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
        /// <exception cref="ServiceNotSupportedException">The cluster does not support Data services.</exception>
        public override async Task<IOperationResult<T>> SendWithDurabilityAsync<T>(IOperation<T> operation, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            IOperationResult<T> result;
            try
            {
                //Is the cluster configured for Data services?
                if (!ConfigInfo.IsDataCapable)
                {
                    throw new ServiceNotSupportedException(
                        ExceptionUtil.GetMessage(ExceptionUtil.ServiceNotSupportedMsg, "Data"));
                }

                result = await SendWithRetryAsync(operation).ContinueOnAnyContext();
                if (result.Success)
                {
                    var config = ConfigInfo.ClientConfig.BucketConfigs[BucketName];

                    if (ConfigInfo.SupportsEnhancedDurability)
                    {
                        var seqnoObserver = new KeySeqnoObserver(ConfigInfo, ClusterController.Transcoder,
                            config.ObserveInterval, (uint) config.ObserveTimeout);

                        var observed = await seqnoObserver.ObserveAsync(result.Token, replicateTo, persistTo)
                            .ContinueOnAnyContext();

                        result.Durability = observed ? Durability.Satisfied : Durability.NotSatisfied;
                        ((OperationResult<T>) result).Success = result.Durability == Durability.Satisfied;
                    }
                    else
                    {
                        var observer = new KeyObserver(ConfigInfo, ClusterController.Transcoder,
                            config.ObserveInterval, config.ObserveTimeout);

                        var observed = await observer.ObserveAsync(operation.Key, result.Cas, deletion, replicateTo, persistTo)
                            .ContinueOnAnyContext();

                        result.Durability = observed ? Durability.Satisfied : Durability.NotSatisfied;
                        ((OperationResult<T>)result).Success = result.Durability == Durability.Satisfied;
                    }
                }
                else
                {
                    result.Durability = Durability.NotSatisfied;
                    ((OperationResult<T>)result).Success = result.Durability == Durability.Satisfied;
                }
            }
            catch (ReplicaNotConfiguredException e)
            {
                result = new OperationResult<T>
                {
                    Exception = e,
                    Status = ResponseStatus.NoReplicasFound,
                    Durability = Durability.NotSatisfied,
                    Success = false
                };
            }
            catch (DocumentMutationLostException e)
            {
                result = new OperationResult<T>
                {
                    Exception = e,
                    Status = ResponseStatus.DocumentMutationLost,
                    Durability = Durability.NotSatisfied,
                    Success = false
                };
            }
            catch (DocumentMutationException e)
            {
                result = new OperationResult<T>
                {
                    Exception = e,
                    Status = ResponseStatus.DocumentMutationDetected,
                    Durability = Durability.NotSatisfied,
                    Success = false
                };
            }
            catch (Exception e)
            {
                result = new OperationResult<T>
                {
                    Exception = e,
                    Status = ResponseStatus.ClientFailure,
                    Success = false
                };
            }
            return result;
        }

        /// <summary>
        /// Sends a View request with retry.
        /// </summary>
        /// <typeparam name="T">The Type T of the <see cref="ViewRow{T}"/> value.</typeparam>
        /// <param name="viewQuery">The view query.</param>
        /// <returns>A <see cref="IViewResult{T}"/> with the results of the query.</returns>
        /// <exception cref="ServiceNotSupportedException">The cluster does not support View services.</exception>
        public override IViewResult<T> SendWithRetry<T>(IViewQueryable viewQuery)
        {
            IViewResult<T> viewResult = null;
            try
            {
                //Is the cluster configured for View services?
                if (!ConfigInfo.IsViewCapable)
                {
                    throw new ServiceNotSupportedException(
                        ExceptionUtil.GetMessage(ExceptionUtil.ServiceNotSupportedMsg, "View"));
                }

                do
                {
                    var server = ConfigInfo.GetViewNode();
                    viewResult = server.Send<T>(viewQuery);
                } while (
                    !viewResult.Success &&
                    !viewResult.CannotRetry() &&
                    viewQuery.RetryAttempts++ <= ConfigInfo.ClientConfig.MaxViewRetries);
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
        /// Sends a <see cref="IOperation" /> to the Couchbase Server using the Memcached protocol.
        /// </summary>
        /// <param name="operation">The <see cref="IOperation" /> to send.</param>
        /// <returns>
        /// An <see cref="IOperationResult" /> with the status of the request.
        /// </returns>
        /// <exception cref="ServiceNotSupportedException">The cluster does not support Data services.</exception>
        public override IOperationResult SendWithRetry(IOperation operation)
        {
            if (Log.IsDebugEnabled && TimingEnabled)
            {
                operation.Timer = Timer;
                operation.BeginTimer(TimingLevel.Three);
            }

            //Is the cluster configured for Data services?
            if (!ConfigInfo.IsDataCapable)
            {
                return  new OperationResult
                {
                    Success = false,
                    Exception = new ServiceNotSupportedException(
                    ExceptionUtil.GetMessage(ExceptionUtil.ServiceNotSupportedMsg, "Data")),
                    Status = ResponseStatus.ClientFailure
                };
            }
            IOperationResult operationResult = new OperationResult { Success = false };
            do
            {
                IVBucket vBucket;
                var server = GetServer(operation.Key, operation.LastConfigRevisionTried, out vBucket);
                if (server == null)
                {
                    continue;
                }
                operation.VBucket = vBucket;
                operation.LastConfigRevisionTried = vBucket.Rev;
                operationResult = server.Send(operation);

                if (operationResult.Success)
                {
                    Log.Debug(
                        m =>
                            m("Operation {0} succeeded {1} for key {2} : {3}", operation.GetType().Name,
                                operation.Attempts, operation.Key, operationResult));
                    break;
                }
                if (CanRetryOperation(operationResult, operation) && !operation.TimedOut())
                {
                    LogFailure(operation, operationResult);
                    operation = operation.Clone();
                    Thread.Sleep((int)Math.Pow(2, operation.Attempts++));
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
                LogFailure(operation, operationResult);
            }

            if (Log.IsDebugEnabled && TimingEnabled)
            {
                operation.EndTimer(TimingLevel.Three);
            }

            return operationResult;
        }

        /// <summary>
        /// Sends a <see cref="IOperation{T}" /> to the Couchbase Server using the Memcached protocol.
        /// </summary>
        /// <typeparam name="T">The Type of the body of the request.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}" /> to send.</param>
        /// <returns>
        /// An <see cref="IOperationResult" /> with the status of the request.
        /// </returns>
        /// <exception cref="ServiceNotSupportedException">The cluster does not support Data services.</exception>
        public override IOperationResult<T> SendWithRetry<T>(IOperation<T> operation)
        {
            if (Log.IsDebugEnabled && TimingEnabled)
            {
                operation.Timer = Timer;
                operation.BeginTimer(TimingLevel.Three);
            }

            //Is the cluster configured for Data services?
            if (!ConfigInfo.IsDataCapable)
            {
                return new OperationResult<T>
                {
                    Success = false,
                    Exception = new ServiceNotSupportedException(
                    ExceptionUtil.GetMessage(ExceptionUtil.ServiceNotSupportedMsg, "Data")),
                    Status = ResponseStatus.ClientFailure
                };
            }

            IOperationResult<T> operationResult = new OperationResult<T> { Success = false };
            do
            {
                IVBucket vBucket;
                var server = GetServer(operation.Key, operation.LastConfigRevisionTried, out vBucket);
                if (server == null)
                {
                    continue;
                }
                operation.VBucket = vBucket;
                operation.LastConfigRevisionTried = vBucket.Rev;
                operationResult = server.Send(operation);

                if (operationResult.Success)
                {
                    Log.Debug(
                        m =>
                            m("Operation {0} succeeded {1} for key {2} : {3}", operation.GetType().Name,
                                operation.Attempts, operation.Key, operationResult.Value));
                    break;
                }
                if(CanRetryOperation(operationResult, operation) && !operation.TimedOut())
                {
                    LogFailure(operation, operationResult);
                    operation = (IOperation<T>)operation.Clone();
                    Thread.Sleep((int)Math.Pow(2, operation.Attempts++));
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
                LogFailure(operation, operationResult);
            }

            if (Log.IsDebugEnabled && TimingEnabled)
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
        /// <exception cref="ServiceNotSupportedException">The cluster does not support View services.</exception>
        public override async Task<IViewResult<T>> SendWithRetryAsync<T>(IViewQueryable query)
        {
            IViewResult<T> viewResult = null;
            try
            {
                //Is the cluster configured for View services?
                if (!ConfigInfo.IsViewCapable)
                {
                    throw new ServiceNotSupportedException(
                        ExceptionUtil.GetMessage(ExceptionUtil.ServiceNotSupportedMsg, "View"));
                }

                using (var cancellationTokenSource = new CancellationTokenSource(ConfigInfo.ClientConfig.ViewRequestTimeout))
                {
                    var task = RetryViewEveryAsync(async (e, c) =>
                    {
                        var server = c.GetViewNode();
                        return await server.SendAsync<T>(query).ContinueOnAnyContext();
                    },
                    query, ConfigInfo, cancellationTokenSource.Token).ConfigureAwait(false);

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
        /// <param name="tcs">The <see cref="TaskCompletionSource{T}"/> the represents the task to await on.</param>
        /// <param name="cts">The <see cref="CancellationTokenSource"/> for cancellation.</param>
        /// <returns>
        /// An <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public override Task<IOperationResult<T>> SendWithRetryAsync<T>(IOperation<T> operation,
            TaskCompletionSource<IOperationResult<T>> tcs = null,
            CancellationTokenSource cts = null)
        {
            tcs = tcs ?? new TaskCompletionSource<IOperationResult<T>>();
            cts = cts ?? new CancellationTokenSource(OperationLifeSpan);

            try
            {
                //Is the cluster configured for Data services?
                if (!ConfigInfo.IsDataCapable)
                {
                    tcs.SetException(new ServiceNotSupportedException(
                        ExceptionUtil.GetMessage(ExceptionUtil.ServiceNotSupportedMsg, "Data")));
                }

                var keyMapper = ConfigInfo.GetKeyMapper();
                var vBucket = (IVBucket) keyMapper.MapKey(operation.Key, operation.LastConfigRevisionTried);
                operation.VBucket = vBucket;
                operation.LastConfigRevisionTried = vBucket.Rev;

                operation.Completed = CallbackFactory.CompletedFuncWithRetryForCouchbase(
                    this, Pending, ClusterController, tcs, cts.Token);

                Pending.TryAdd(operation.Opaque, operation);

                IServer server;
                var attempts = 0;
                while ((server = vBucket.LocatePrimary()) == null)
                {
                    if (attempts++ > 10) { throw new TimeoutException("Could not acquire a server.");}
                    Thread.Sleep((int)Math.Pow(2, attempts));
                }
                server.SendAsync(operation).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                tcs.TrySetResult(new OperationResult<T>
                {
                    Exception = e,
                    Status = ResponseStatus.ClientFailure
                });
            }
            return tcs.Task;
        }


        /// <summary>
        /// Sends a <see cref="IOperation" /> to the Couchbase Server using the Memcached protocol using async/await.
        /// </summary>
        /// <param name="operation">The <see cref="IOperation" /> to send.</param>
        /// <param name="tcs">The <see cref="TaskCompletionSource{T}"/> the represents the task to await on.</param>
        /// <param name="cts">The <see cref="CancellationTokenSource"/> for cancellation.</param>
        /// <returns>
        /// An <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public override Task<IOperationResult> SendWithRetryAsync(IOperation operation,
            TaskCompletionSource<IOperationResult> tcs = null,
            CancellationTokenSource cts = null)
        {
            tcs = tcs ?? new TaskCompletionSource<IOperationResult>();
            cts = cts ?? new CancellationTokenSource(OperationLifeSpan);

            try
            {
                //Is the cluster configured for Data services?
                if (!ConfigInfo.IsDataCapable)
                {
                    tcs.SetException(new ServiceNotSupportedException(
                        ExceptionUtil.GetMessage(ExceptionUtil.ServiceNotSupportedMsg, "Data")));
                }

                var keyMapper = ConfigInfo.GetKeyMapper();
                var vBucket = (IVBucket) keyMapper.MapKey(operation.Key, operation.LastConfigRevisionTried);
                operation.VBucket = vBucket;
                operation.LastConfigRevisionTried = vBucket.Rev;

                operation.Completed = CallbackFactory.CompletedFuncWithRetryForCouchbase(
                    this, Pending, ClusterController, tcs, cts.Token);

                Pending.TryAdd(operation.Opaque, operation);

                IServer server;
                var attempts = 0;
                while ((server = vBucket.LocatePrimary()) == null)
                {
                    if (attempts++ > 10) { throw new TimeoutException("Could not acquire a server."); }
                    Thread.Sleep((int)Math.Pow(2, attempts));
                }
                Log.Debug(m=>m("Starting send for {0} with {1}", operation.Opaque, server.EndPoint));
                server.SendAsync(operation).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                tcs.TrySetResult(new OperationResult
                {
                    Exception = e,
                    Status = ResponseStatus.ClientFailure
                });
            }
            return tcs.Task;
        }

        public override ISearchQueryResult SendWithRetry(SearchQuery searchQuery)
        {
            ISearchQueryResult searchResult = null;
            try
            {
                if (!ConfigInfo.IsSearchCapable)
                {
                    throw new ServiceNotSupportedException
                      (ExceptionUtil.GetMessage(ExceptionUtil.ServiceNotSupportedMsg, "FTS"));
                }

                var attempts = 0;
                IServer server;
                while ((server = ConfigInfo.GetSearchNode()) == null)
                {
                    if (attempts++ > 10) { throw new TimeoutException("Could not acquire a server."); }
                    Thread.Sleep((int)Math.Pow(2, attempts));
                }

                searchResult = server.Send(searchQuery);
            }
            catch (Exception e)
            {
                Log.Info(e);
                searchResult = new SearchQueryResult
                {
                    Status = SearchStatus.Failed,
                    Success = false,
                    Exception = e
                };
            }
            return searchResult;
        }

        public override async Task<ISearchQueryResult> SendWithRetryAsync(SearchQuery searchQuery)
        {
            ISearchQueryResult searchResult = null;
            try
            {
                if (!ConfigInfo.IsSearchCapable)
                {
                    throw new ServiceNotSupportedException
                      (ExceptionUtil.GetMessage(ExceptionUtil.ServiceNotSupportedMsg, "FTS"));
                }

                var attempts = 0;
                IServer server;
                while ((server = ConfigInfo.GetSearchNode()) == null)
                {
                    if (attempts++ > 10) { throw new TimeoutException("Could not acquire a server."); }
                    Thread.Sleep((int)Math.Pow(2, attempts));
                }

                searchResult = await server.SendAsync(searchQuery);
            }
            catch (Exception e)
            {
                Log.Info(e);
                searchResult = new SearchQueryResult
                {
                    Status = SearchStatus.Failed,
                    Success = false,
                    Exception = e
                };
            }
            return searchResult;
        }

        /// <summary>
        /// Sends a N1QL query to the server to be executed using the <see cref="IQueryRequest" /> object.
        /// </summary>
        /// <typeparam name="T">The Type T of the body of each result row.</typeparam>
        /// <param name="queryRequest">The <see cref="IQueryRequest" /> object to send to the server.</param>
        /// <returns>
        /// An <see cref="IQueryResult{T}" /> object that is the result of the query.
        /// </returns>
        /// <exception cref="ServiceNotSupportedException">The cluster does not support Query services.</exception>
        public override IQueryResult<T> SendWithRetry<T>(IQueryRequest queryRequest)
        {
            IQueryResult<T> queryResult = null;
            try
            {
                //Is the cluster configured for Query services?
                if (!ConfigInfo.IsQueryCapable)
                {
                    throw new ServiceNotSupportedException
                        (ExceptionUtil.GetMessage(ExceptionUtil.ServiceNotSupportedMsg, "Data"));
                }

                queryRequest.Lifespan = new Lifespan
                {
                    CreationTime = DateTime.UtcNow,
                    Duration = ConfigInfo.ClientConfig.QueryRequestTimeout
                };
                do
                {
                    var attempts = 0;
                    IServer server;
                    while ((server = ConfigInfo.GetQueryNode()) == null)
                    {
                        if (attempts++ > 10) { throw new TimeoutException("Could not acquire a server."); }
                        Thread.Sleep((int)Math.Pow(2, attempts));
                    }

                    queryResult = server.Send<T>(queryRequest);

                    //if this is too loose, we may need to constrain it to HttpRequestException or another exception later on
                    var exception = queryResult.Exception;
                    if (exception != null)
                    {
                        UpdateConfig();
                    }
                } while (!queryResult.Success && queryResult.ShouldRetry() && !queryRequest.TimedOut());
            }
            catch (Exception e)
            {
                Log.Info(e);
                const string message = "The query request failed, check Error and Exception fields for details.";
                queryResult = new QueryResult<T>
                {
                    Message = message,
                    Status = QueryStatus.Fatal,
                    Success = false,
                    Exception = e
                };
            }
            return queryResult;
        }

        /// <summary>
        /// Sends a N1QL query to the server to be executed using the <see cref="IQueryRequest"/> object using async/await.
        /// </summary>
        /// <typeparam name="T">The Type T of the body of each result row.</typeparam>
        /// <param name="queryRequest">The <see cref="IQueryRequest"/> object to send to the server.</param>
        /// <returns>An <see cref="Task{IQueryResult}"/> object to be awaited on that is the result of the query.</returns>
        /// <exception cref="ServiceNotSupportedException">The cluster does not support Query services.</exception>
        public override async Task<IQueryResult<T>> SendWithRetryAsync<T>(IQueryRequest queryRequest)
        {
            IQueryResult<T> queryResult = null;
            try
            {
                //Is the cluster configured for Query services?
                if (!ConfigInfo.IsQueryCapable)
                {
                    throw new ServiceNotSupportedException(
                        ExceptionUtil.GetMessage(ExceptionUtil.ServiceNotSupportedMsg, "Query"));
                }

                queryRequest.Lifespan = new Lifespan
                {
                    CreationTime = DateTime.UtcNow,
                    Duration = ConfigInfo.ClientConfig.QueryRequestTimeout
                };

                using (var cancellationTokenSource = new CancellationTokenSource(ConfigInfo.ClientConfig.ViewRequestTimeout))
                {
                    queryResult = await RetryQueryEveryAsync(async (e, c) =>
                    {
                        var attempts = 0;
                        IServer server;
                        while ((server = c.GetQueryNode()) == null)
                        {
                            if (attempts++ > 10) { throw new TimeoutException("Could not acquire a server."); }
                            Thread.Sleep((int)Math.Pow(2, attempts));
                        }

                        var result = await server.SendAsync<T>(queryRequest).ContinueOnAnyContext();
                        if (!result.Success)
                        {
                            //if this is too loose, we may need to constrain it to HttpRequestException or another exception later on
                            var exception = result.Exception;
                            if (exception != null)
                            {
                                lock (_syncObj)
                                {
                                    Log.Trace("Request failed checking config:", exception);
                                    UpdateConfig();
                                }
                            }
                        }
                        return result;
                    },
                    queryRequest, ConfigInfo, cancellationTokenSource.Token).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                Log.Info(e);
                const string message = "The Query request failed, check Error and Exception fields for details.";
                queryResult = new QueryResult<T>
                {
                    Message = message,
                    Status = QueryStatus.Fatal,
                    Success = false,
                    Exception = e
                };
            }
            return queryResult;
        }
    }
}