using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Logging;
using Couchbase.Configuration;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Services;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.N1QL;
using Couchbase.Search;
using Couchbase.Tracing;
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
        private new static readonly ILog Log = LogManager.GetLogger<CouchbaseRequestExecuter>();

        //for log redaction
        private Func<object, string> User = RedactableArgument.UserAction;

        public CouchbaseRequestExecuter(IClusterController clusterController, IConfigInfo configInfo,
            string bucketName, ConcurrentDictionary<uint, IOperation> pending)
            : base(clusterController, configInfo, bucketName, pending)
        {
        }

        /// <summary>
        /// Checks the <see cref="IOperation"/> to see if it supports retries and then checks the <see cref="IOperationResult"/>
        ///  to see if the error or server response supports retries.
        /// </summary>
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
                var bucketConfig = operation.GetConfig(ClusterController.ServerConfigTranscoder);
                if (bucketConfig != null)
                {
                    Log.Info("New config found {0}|{1}", bucketConfig.Rev, ConfigInfo.BucketConfig.Rev);
                    Log.Debug("{0}", JsonConvert.SerializeObject(bucketConfig));
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
            //Validate key length
            operation.Validate();

            using (Tracer.StartParentScope(operation, ConfigInfo.BucketName, true))
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
                        if (replicateTo == ReplicateTo.Zero && persistTo == PersistTo.Zero)
                        {
                            // don't dispatch observe if we don't need to
                            result.Durability = Durability.Satisfied;
                        }
                        else
                        {
                            var config = ConfigInfo.ClientConfig.BucketConfigs[BucketName];

                            if (ConfigInfo.SupportsEnhancedDurability)
                            {
                                var seqnoObserver = new KeySeqnoObserver(operation.Key, Pending, ConfigInfo, ClusterController,
                                    config.ObserveInterval, (uint)config.ObserveTimeout);

                                var observed = seqnoObserver.Observe(result.Token, replicateTo, persistTo);
                                result.Durability = observed ? Durability.Satisfied : Durability.NotSatisfied;
                            }
                            else
                            {
                                var observer = new KeyObserver(Pending, ConfigInfo, ClusterController, config.ObserveInterval, config.ObserveTimeout);
                                var observed = observer.Observe(operation.Key, result.Cas, deletion, replicateTo, persistTo);
                                result.Durability = observed ? Durability.Satisfied : Durability.NotSatisfied;
                            }
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
                        Id = operation.Key,
                        Exception = e,
                        Status = ResponseStatus.NoReplicasFound,
                        Durability = Durability.NotSatisfied
                    };
                }
                catch (DocumentMutationLostException e)
                {
                    result = new OperationResult<T>
                    {
                        Id = operation.Key,
                        Exception = e,
                        Status = ResponseStatus.DocumentMutationLost,
                        Durability = Durability.NotSatisfied
                    };
                }
                catch (Exception e)
                {
                    result = new OperationResult<T>
                    {
                        Id = operation.Key,
                        Exception = e,
                        Status = ResponseStatus.ClientFailure
                    };
                }

                return result;
            }
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
            //Validate key length
            operation.Validate();

            using (Tracer.StartParentScope(operation, ConfigInfo.BucketName, true))
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
                        if (replicateTo == ReplicateTo.Zero && persistTo == PersistTo.Zero)
                        {
                            // don't dispatch observe if we don't need to
                            result.Durability = Durability.Satisfied;
                        }
                        else
                        {
                            var config = ConfigInfo.ClientConfig.BucketConfigs[BucketName];

                            if (ConfigInfo.SupportsEnhancedDurability)
                            {
                                var seqnoObserver = new KeySeqnoObserver(operation.Key, Pending, ConfigInfo, ClusterController,
                                    config.ObserveInterval, (uint)config.ObserveTimeout);

                                var observed = seqnoObserver.Observe(result.Token, replicateTo, persistTo);
                                result.Durability = observed ? Durability.Satisfied : Durability.NotSatisfied;
                            }
                            else
                            {
                                var observer = new KeyObserver(Pending, ConfigInfo, ClusterController, config.ObserveInterval, config.ObserveTimeout);
                                var observed = observer.Observe(operation.Key, result.Cas, deletion, replicateTo, persistTo);
                                result.Durability = observed ? Durability.Satisfied : Durability.NotSatisfied;
                            }
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
                        Id = operation.Key,
                        Exception = e,
                        Status = ResponseStatus.NoReplicasFound,
                        Durability = Durability.NotSatisfied,
                        OpCode = operation.OperationCode
                    };
                }
                catch (DocumentMutationLostException e)
                {
                    result = new OperationResult
                    {
                        Id = operation.Key,
                        Exception = e,
                        Status = ResponseStatus.DocumentMutationLost,
                        Durability = Durability.NotSatisfied,
                        OpCode = operation.OperationCode
                    };
                }
                catch (Exception e)
                {
                    result = new OperationResult
                    {
                        Id = operation.Key,
                        Exception = e,
                        Status = ResponseStatus.ClientFailure,
                        OpCode = operation.OperationCode
                    };
                }

                return result;
            }
        }

        /// <summary>
        /// Sends an operation to the server while observing it's durability requirements using async/await
        /// </summary>
        /// <typeparam name="T">The value for T.</typeparam>
        /// <param name="operation">A binary memcached operation - must be a mutation operation.</param>
        /// <param name="deletion">True if mutation is a deletion.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="tcs">The <see cref="TaskCompletionSource{T}"/> the represents the task to await on.</param>
        /// <param name="cts">The <see cref="CancellationTokenSource"/> for cancellation.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> to be awaited on with it's <see cref="Durability"/> status.</returns>
        /// <exception cref="ServiceNotSupportedException">The cluster does not support Data services.</exception>
        public override async Task<IOperationResult<T>> SendWithDurabilityAsync<T>(IOperation<T> operation,
            bool deletion, ReplicateTo replicateTo, PersistTo persistTo, TaskCompletionSource<IOperationResult<T>> tcs = null,
            CancellationTokenSource cts = null)
        {
            //Validate key length
            operation.Validate();

            using (Tracer.StartParentScope(operation, ConfigInfo.BucketName, true))
            {
                tcs = tcs ?? new TaskCompletionSource<IOperationResult<T>>();

                IOperationResult<T> result;
                try
                {
                    //Is the cluster configured for Data services?
                    if (!ConfigInfo.IsDataCapable)
                    {
                        throw new ServiceNotSupportedException(
                            ExceptionUtil.GetMessage(ExceptionUtil.ServiceNotSupportedMsg, "Data"));
                    }

                    result = await SendWithRetryAsync(operation, tcs).ContinueOnAnyContext();
                    if (result.Success)
                    {
                        if (replicateTo == ReplicateTo.Zero && persistTo == PersistTo.Zero)
                        {
                            // don't dispatch observe if we don't need to
                            result.Durability = Durability.Satisfied;
                        }
                        else
                        {
                            var config = ConfigInfo.ClientConfig.BucketConfigs[BucketName];
                            cts = cts ?? new CancellationTokenSource(config.ObserveTimeout);

                            using (cts)
                            {
                                if (ConfigInfo.SupportsEnhancedDurability)
                                {
                                    var seqnoObserver = new KeySeqnoObserver(operation.Key, Pending, ConfigInfo,
                                        ClusterController,
                                        config.ObserveInterval, (uint)config.ObserveTimeout);

                                    var observed = await seqnoObserver.ObserveAsync(result.Token, replicateTo, persistTo, cts)
                                        .ContinueOnAnyContext();

                                    result.Durability = observed ? Durability.Satisfied : Durability.NotSatisfied;
                                    ((OperationResult<T>)result).Success = result.Durability == Durability.Satisfied;
                                }
                                else
                                {
                                    var observer = new KeyObserver(Pending, ConfigInfo, ClusterController,
                                        config.ObserveInterval, config.ObserveTimeout);

                                    var observed = await observer.ObserveAsync(operation.Key, result.Cas,
                                        deletion, replicateTo, persistTo, cts).ContinueOnAnyContext();

                                    result.Durability = observed ? Durability.Satisfied : Durability.NotSatisfied;
                                    ((OperationResult<T>)result).Success = result.Durability == Durability.Satisfied;
                                }
                            }
                        }
                    }
                    else
                    {
                        result.Durability = Durability.NotSatisfied;
                        ((OperationResult<T>)result).Success = result.Durability == Durability.Satisfied;
                    }
                }
                catch (TaskCanceledException e)
                {
                    result = new OperationResult<T>
                    {
                        Id = operation.Key,
                        Exception = e,
                        Status = ResponseStatus.OperationTimeout,
                        Durability = Durability.NotSatisfied,
                        Success = false
                    };
                }
                catch (ReplicaNotConfiguredException e)
                {
                    result = new OperationResult<T>
                    {
                        Id = operation.Key,
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
                        Id = operation.Key,
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
                        Id = operation.Key,
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
                        Id = operation.Key,
                        Exception = e,
                        Status = ResponseStatus.ClientFailure,
                        Success = false
                    };
                }

                return result;
            }
        }

        /// <summary>
        /// Sends an operation to the server while observing its durability requirements using async/await
        /// </summary>
        /// <param name="operation">A binary memcached operation - must be a mutation operation.</param>
        /// <param name="deletion">True if mutation is a deletion.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="tcs">The <see cref="TaskCompletionSource{T}"/> the represents the task to await on.</param>
        /// <param name="cts">The <see cref="CancellationTokenSource"/> for cancellation.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> to be awaited on with its <see cref="Durability"/> status.</returns>
        /// <exception cref="ServiceNotSupportedException">The cluster does not support Data services.</exception>
        public override async Task<IOperationResult> SendWithDurabilityAsync(IOperation operation, bool deletion,
            ReplicateTo replicateTo, PersistTo persistTo, TaskCompletionSource<IOperationResult> tcs = null,
            CancellationTokenSource cts = null)
        {
            //Validate key length
            operation.Validate();

            using (Tracer.StartParentScope(operation, ConfigInfo.BucketName, true))
            {
                var config = ConfigInfo.ClientConfig.BucketConfigs[BucketName];
                tcs = tcs ?? new TaskCompletionSource<IOperationResult>();
                cts = cts ?? new CancellationTokenSource(config.ObserveTimeout);

                IOperationResult result;
                try
                {
                    //Is the cluster configured for Data services?
                    if (!ConfigInfo.IsDataCapable)
                    {
                        throw new ServiceNotSupportedException(
                            ExceptionUtil.GetMessage(ExceptionUtil.ServiceNotSupportedMsg, "Data"));
                    }

                    result = await SendWithRetryAsync(operation, tcs).ContinueOnAnyContext();
                    if (result.Success)
                    {
                        if (replicateTo == ReplicateTo.Zero && persistTo == PersistTo.Zero)
                        {
                            // don't dispatch observe if we don't need to
                            result.Durability = Durability.Satisfied;
                        }
                        else
                        {
                            using (cts)
                            {
                                if (ConfigInfo.SupportsEnhancedDurability)
                                {
                                    var seqnoObserver = new KeySeqnoObserver(operation.Key, Pending, ConfigInfo,
                                        ClusterController,
                                        config.ObserveInterval, (uint)config.ObserveTimeout);

                                    var observed = await seqnoObserver.ObserveAsync(result.Token, replicateTo, persistTo, cts)
                                        .ContinueOnAnyContext();

                                    result.Durability = observed ? Durability.Satisfied : Durability.NotSatisfied;
                                    ((OperationResult)result).Success = result.Durability == Durability.Satisfied;
                                }
                                else
                                {
                                    var observer = new KeyObserver(Pending, ConfigInfo, ClusterController,
                                        config.ObserveInterval, config.ObserveTimeout);

                                    var observed = await observer.ObserveAsync(operation.Key, result.Cas,
                                        deletion, replicateTo, persistTo, cts).ContinueOnAnyContext();

                                    result.Durability = observed ? Durability.Satisfied : Durability.NotSatisfied;
                                    ((OperationResult)result).Success = result.Durability == Durability.Satisfied;
                                }
                            }
                        }
                    }
                    else
                    {
                        result.Durability = Durability.NotSatisfied;
                        ((OperationResult)result).Success = result.Durability == Durability.Satisfied;
                    }
                }
                catch (TaskCanceledException e)
                {
                    result = new OperationResult
                    {
                        Id = operation.Key,
                        Exception = e,
                        Status = ResponseStatus.OperationTimeout,
                        Durability = Durability.NotSatisfied,
                        Success = false
                    };
                }
                catch (ReplicaNotConfiguredException e)
                {
                    result = new OperationResult
                    {
                        Id = operation.Key,
                        Exception = e,
                        Status = ResponseStatus.NoReplicasFound,
                        Durability = Durability.NotSatisfied,
                        Success = false
                    };
                }
                catch (DocumentMutationLostException e)
                {
                    result = new OperationResult
                    {
                        Id = operation.Key,
                        Exception = e,
                        Status = ResponseStatus.DocumentMutationLost,
                        Durability = Durability.NotSatisfied,
                        Success = false
                    };
                }
                catch (DocumentMutationException e)
                {
                    result = new OperationResult
                    {
                        Id = operation.Key,
                        Exception = e,
                        Status = ResponseStatus.DocumentMutationDetected,
                        Durability = Durability.NotSatisfied,
                        Success = false
                    };
                }
                catch (Exception e)
                {
                    result = new OperationResult
                    {
                        Id = operation.Key,
                        Exception = e,
                        Status = ResponseStatus.ClientFailure,
                        Success = false
                    };
                }

                return result;
            }
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
            //Validate key length
            operation.Validate();

            //Is the cluster configured for Data services?
            if (!ConfigInfo.IsDataCapable)
            {
                return new OperationResult
                {
                    Id = operation.Key,
                    Success = false,
                    Exception = new ServiceNotSupportedException(
                    ExceptionUtil.GetMessage(ExceptionUtil.ServiceNotSupportedMsg, "Data")),
                    Status = ResponseStatus.ClientFailure,
                    OpCode = operation.OperationCode
                };
            }
            using (Tracer.StartParentScope(operation, ConfigInfo.BucketName))
            {
                IOperationResult operationResult = new OperationResult { Success = false, OpCode = operation.OperationCode };
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
                    operation.Attempts++;

                    if (operationResult.Success)
                    {
                        Log.Debug(
                            "Operation {0} succeeded {1} for key {2} : {3}", operation.GetType().Name,
                            operation.Attempts, operation.Key, operationResult);
                        break;
                    }
                    if (CanRetryOperation(operationResult, operation) && !operation.TimedOut())
                    {
                        LogFailure(operation, operationResult);
                        operation = operation.Clone();

                        // Get retry timeout, uses default timeout if no retry stratergy available
                        Thread.Sleep(operation.GetRetryTimeout(VBucketRetrySleepTime));
                    }
                    else
                    {
                        ((OperationResult)operationResult).SetException();
                        Log.Debug("Operation doesn't support retries for key {0}", operation.Key);
                        break;
                    }
                } while (!operationResult.Success && !operation.TimedOut());

                if (!operationResult.Success)
                {
                    if (operation.TimedOut() && operationResult.ShouldRetry())
                    {
                        const string msg = "The operation has timed out.";
                        ((OperationResult)operationResult).Message = msg;
                        ((OperationResult)operationResult).Status = ResponseStatus.OperationTimeout;
                    }
                    LogFailure(operation, operationResult);
                }

                return operationResult;
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
        /// <exception cref="ServiceNotSupportedException">The cluster does not support Data services.</exception>
        public override IOperationResult<T> SendWithRetry<T>(IOperation<T> operation)
        {
            //Validate key length
            operation.Validate();

            //Is the cluster configured for Data services?
            if (!ConfigInfo.IsDataCapable)
            {
                return new OperationResult<T>
                {
                    Id = operation.Key,
                    Success = false,
                    Exception = new ServiceNotSupportedException(
                    ExceptionUtil.GetMessage(ExceptionUtil.ServiceNotSupportedMsg, "Data")),
                    Status = ResponseStatus.ClientFailure
                };
            }

            using (Tracer.StartParentScope(operation, ConfigInfo.BucketName))
            {
                IOperationResult<T> operationResult = new OperationResult<T> { Success = false, OpCode = operation.OperationCode };
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
                    operation.Attempts++;

                    if (operationResult.Success)
                    {
                        Log.Debug(
                            "Operation {0} succeeded {1} for key {2} : {3}", operation.GetType().Name,
                            operation.Attempts, operation.Key, operationResult.Value);
                        break;
                    }
                    if (CanRetryOperation(operationResult, operation) && !operation.TimedOut())
                    {
                        LogFailure(operation, operationResult);
                        operation = (IOperation<T>)operation.Clone();

                        // Get retry timeout, uses default timeout if no retry stratergy available
                        Thread.Sleep(operation.GetRetryTimeout(VBucketRetrySleepTime));
                    }
                    else
                    {
                        ((OperationResult)operationResult).SetException();
                        Log.Debug("Operation doesn't support retries for key {0}", operation.Key);
                        break;
                    }
                } while (!operationResult.Success && !operation.TimedOut());

                if (!operationResult.Success)
                {
                    if (operation.TimedOut() && operationResult.ShouldRetry())
                    {
                        const string msg = "The operation has timed out.";
                        ((OperationResult)operationResult).Message = msg;
                        ((OperationResult)operationResult).Status = ResponseStatus.OperationTimeout;
                    }
                    LogFailure(operation, operationResult);
                }

                return operationResult;
            }
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
        public override async Task<IOperationResult<T>> SendWithRetryAsync<T>(IOperation<T> operation,
            TaskCompletionSource<IOperationResult<T>> tcs = null,
            CancellationTokenSource cts = null)
        {
            //Validate key length
            operation.Validate();

            using (Tracer.StartParentScope(operation, ConfigInfo.BucketName))
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
                    var vBucket = (IVBucket)keyMapper.MapKey(operation.Key, operation.LastConfigRevisionTried);
                    operation.VBucket = vBucket;
                    operation.LastConfigRevisionTried = vBucket.Rev;

                    operation.Completed = CallbackFactory.CompletedFuncWithRetryForCouchbase(
                        this, Pending, ClusterController, tcs, cts.Token);

                    Pending.TryAdd(operation.Opaque, operation);

                    var server = await GetServerWithRetryAsync(vBucket.LocatePrimary, cts.Token).ContinueOnAnyContext();
                    await server.SendAsync(operation).ContinueOnAnyContext();
                }
                catch (Exception e)
                {
                    tcs.TrySetResult(new OperationResult<T>
                    {
                        Id = operation.Key,
                        Exception = e,
                        Status = ResponseStatus.ClientFailure
                    });
                }
            }

            return await tcs.Task.ContinueOnAnyContext();
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
        public override async Task<IOperationResult> SendWithRetryAsync(IOperation operation,
            TaskCompletionSource<IOperationResult> tcs = null,
            CancellationTokenSource cts = null)
        {
            //Validate key length
            operation.Validate();

            using (Tracer.StartParentScope(operation, ConfigInfo.BucketName))
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
                    var vBucket = (IVBucket)keyMapper.MapKey(operation.Key, operation.LastConfigRevisionTried);
                    operation.VBucket = vBucket;
                    operation.LastConfigRevisionTried = vBucket.Rev;

                    operation.Completed = CallbackFactory.CompletedFuncWithRetryForCouchbase(
                        this, Pending, ClusterController, tcs, cts.Token);

                    Pending.TryAdd(operation.Opaque, operation);

                    var server = await GetServerWithRetryAsync(vBucket.LocatePrimary, cts.Token);
                    Log.Debug("Starting send for {0} with {1}", operation.Opaque, server.EndPoint);
                    await server.SendAsync(operation).ContinueOnAnyContext();
                }
                catch (Exception e)
                {
                    tcs.TrySetResult(new OperationResult
                    {
                        Id = operation.Key,
                        Exception = e,
                        Status = ResponseStatus.ClientFailure
                    });
                }
            }

            return await tcs.Task.ContinueOnAnyContext();
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
            using (Tracer.StartParentScope(viewQuery))
            {
                IViewResult<T> viewResult;
                try
                {
                    EnsureNotEphemeral(ConfigInfo.BucketConfig.BucketType);
                    EnsureServiceAvailable(ConfigInfo.IsViewCapable, "View");

                    viewResult = RetryRequest(
                        ConfigInfo.GetViewNode,
                        (server, request) => server.Send<T>(request),
                        (request, result) =>
                        {
                            if (!(result.Success || !result.ShouldRetry() || request.RetryAttempts >= ConfigInfo.ClientConfig.MaxViewRetries))
                            {
                                request.RetryAttempts++;
                                return true;
                            }
                            return false;
                        },
                        viewQuery
                    );
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
            using (Tracer.StartParentScope(query))
            {
                IViewResult<T> viewResult;
                try
                {
                    EnsureNotEphemeral(ConfigInfo.BucketConfig.BucketType);
                    EnsureServiceAvailable(ConfigInfo.IsViewCapable, "View");

                    viewResult = await RetryRequestAsync(
                        ConfigInfo.GetViewNode,
                        (server, request, token) =>
                        {
                            request.RetryAttempts++;
                            return server.SendAsync<T>(request);
                        },
                        (request, result) => !(result.Success || !result.ShouldRetry() || request.RetryAttempts >= ConfigInfo.ClientConfig.MaxViewRetries),
                        query,
                        CancellationToken.None,
                        ConfigInfo.ClientConfig.ViewRequestTimeout
                    ).ContinueOnAnyContext();
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
        }

        public override ISearchQueryResult SendWithRetry(SearchQuery searchQuery)
        {
            using (Tracer.StartParentScope(searchQuery))
            {
                ISearchQueryResult searchResult;
                try
                {
                    EnsureServiceAvailable(ConfigInfo.IsSearchCapable, "FTS");

                    searchResult = RetryRequest(
                        ConfigInfo.GetSearchNode,
                        (server, request) => server.Send(request),
                        (request, result) => !result.Success && result.ShouldRetry(),
                        searchQuery
                    );
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
        }

        public override async Task<ISearchQueryResult> SendWithRetryAsync(SearchQuery searchQuery, CancellationToken cancellationToken)
        {
            using (Tracer.StartParentScope(searchQuery))
            {
                ISearchQueryResult searchResult;
                try
                {
                    EnsureServiceAvailable(ConfigInfo.IsSearchCapable, "FTS");

                    searchResult = await RetryRequestAsync(
                        ConfigInfo.GetSearchNode,
                        (server, request, token) => server.SendAsync(request, cancellationToken),
                        (request, result) => !result.Success && result.ShouldRetry(),
                        searchQuery,
                        cancellationToken,
                        (int)ConfigInfo.ClientConfig.SearchRequestTimeout
                    ).ContinueOnAnyContext();
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
            using (Tracer.StartParentScope(queryRequest))
            {
                IQueryResult<T> queryResult;
                try
                {
                    EnsureServiceAvailable(ConfigInfo.IsQueryCapable, "Query");

                    queryRequest.Lifespan = new Lifespan
                    {
                        CreationTime = DateTime.UtcNow,
                        Duration = ConfigInfo.ClientConfig.QueryRequestTimeout
                    };

                    queryResult = RetryRequest(
                        ConfigInfo.GetQueryNode,
                        (server, req) => server.Send<T>(req),
                        (req, res) => !(res.Success || req.TimedOut()) && res.ShouldRetry(),
                        queryRequest
                    );
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
        }

        /// <summary>
        /// Sends a N1QL query to the server to be executed using the <see cref="IQueryRequest"/> object using async/await.
        /// </summary>
        /// <typeparam name="T">The Type T of the body of each result row.</typeparam>
        /// <param name="queryRequest">The <see cref="IQueryRequest"/> object to send to the server.</param>
        /// <param name="cancellationToken">Token which can cancel the query.</param>
        /// <returns>An <see cref="Task{IQueryResult}"/> object to be awaited on that is the result of the query.</returns>
        /// <exception cref="ServiceNotSupportedException">The cluster does not support Query services.</exception>
        public override async Task<IQueryResult<T>> SendWithRetryAsync<T>(IQueryRequest queryRequest, CancellationToken cancellationToken)
        {
            using (Tracer.StartParentScope(queryRequest))
            {
                IQueryResult<T> queryResult;
                try
                {
                    EnsureServiceAvailable(ConfigInfo.IsQueryCapable, "Query");

                    queryRequest.Lifespan = new Lifespan
                    {
                        CreationTime = DateTime.UtcNow,
                        Duration = ConfigInfo.ClientConfig.QueryRequestTimeout
                    };

                    queryResult = await RetryRequestAsync(
                        ConfigInfo.GetQueryNode,
                        (server, request, token) => server.SendAsync<T>(request, token),
                        (request, result) => !(result.Success || request.TimedOut()) && result.ShouldRetry(),
                        queryRequest,
                        cancellationToken,
                        (int) ConfigInfo.ClientConfig.QueryRequestTimeout
                    ).ContinueOnAnyContext();
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

        #region CBAS

        /// <summary>
        /// Sends an <see cref="IAnalyticsResult{T}"/> to the server to be executed.
        /// </summary>
        /// <typeparam name="T">The Type T of the body of each result row.</typeparam>
        /// <param name="request">The request.</param>
        /// <returns></returns>
        /// <exception cref="System.TimeoutException">Could not acquire a server.</exception>
        public override IAnalyticsResult<T> SendWithRetry<T>(IAnalyticsRequest request)
        {
            using (Tracer.StartParentScope(request))
            {
                IAnalyticsResult<T> result;
                try
                {
                    EnsureServiceAvailable(ConfigInfo.IsAnalyticsCapable, "Analytics");

                    // Ugly but prevents Lifespan being public on IAnalyticsRequest
                    ((AnalyticsRequest)request).ConfigureLifespan(ConfigInfo.ClientConfig.AnalyticsRequestTimeout);

                    result = RetryRequest(
                        ConfigInfo.GetAnalyticsNode,
                        (server, req) => server.Send<T>(req),
                        (req, res) => !(res.Success || req.TimedOut()) && res.ShouldRetry(),
                        request);
                }
                catch (Exception exception)
                {
                    Log.Info(exception);
                    result = CreateFailedAnalyticsResult<T>(exception);
                }

                return result;
            }
        }

        /// <summary>
        /// Asynchronously sends an <see cref="IAnalyticsResult{T}"/> to the server to be executed.
        /// </summary>
        /// <typeparam name="T">The Type T of the body of each result row.</typeparam>
        /// <param name="request">The <see cref="IAnalyticsRequest"/> object to send to the server.</param>
        /// <param name="cancellationToken">Token which can cancel the analytics request.</param>
        /// <returns>An <see cref="Task{IAnalyticsRequest}"/> object to be awaited on that is the result of the analytics request.</returns>
        /// <exception cref="ServiceNotSupportedException">The cluster does not support analytics services.</exception>
        public override async Task<IAnalyticsResult<T>> SendWithRetryAsync<T>(IAnalyticsRequest request, CancellationToken cancellationToken)
        {
            using (Tracer.StartParentScope(request))
            {
                IAnalyticsResult<T> result;
                try
                {
                    EnsureServiceAvailable(ConfigInfo.IsAnalyticsCapable, "Analytics");

                    // Ugly but prevents Lifespan being public
                    ((AnalyticsRequest)request).ConfigureLifespan(ConfigInfo.ClientConfig.QueryRequestTimeout);

                    result = await RetryRequestAsync(
                        ConfigInfo.GetAnalyticsNode,
                        (server, req, token) => server.SendAsync<T>(req, token),
                        (req, res) => !(res.Success || req.TimedOut()) && res.ShouldRetry(),
                        request,
                        cancellationToken,
                        (int)ConfigInfo.ClientConfig.AnalyticsRequestTimeout
                    ).ContinueOnAnyContext();
                }
                catch (Exception exception)
                {
                    Log.Info(exception);
                    result = CreateFailedAnalyticsResult<T>(exception);
                }

                return result;
            }
        }

        private static IAnalyticsResult<T> CreateFailedAnalyticsResult<T>(Exception exception)
        {
            const string message = "The Analytics request failed, check Error and Exception fields for details.";
            return new AnalyticsResult<T>
            {
                Success = false,
                Status = QueryStatus.Fatal,
                Message = message,
                Exception = exception
            };
        }

        #endregion
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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

#endregion
