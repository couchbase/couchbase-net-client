using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Logging;
using Couchbase.Configuration;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.Utils;
using Couchbase.Tracing;

namespace Couchbase.Core.Buckets
{
    /// <summary>
    /// An implementation of <see cref="IRequestExecuter"/> for executing memcached specific operations against an
    /// in-memory, Memcached Bucket in a Couchbase cluster.
    /// </summary>
    /// <remarks>Note that the only methods which Memcached buckets support are implemented.
    /// Methods that are not implemented may throw a <see cref="NotSupportedException"/>.</remarks>
    internal class MemcachedRequestExecuter : RequestExecuterBase
    {
        protected static readonly new ILog Log = LogManager.GetLogger<MemcachedRequestExecuter>();

        //for log redaction
        private Func<object, string> User = RedactableArgument.UserAction;

        public MemcachedRequestExecuter(IClusterController clusterController, IConfigInfo configInfo,
            string bucketName, ConcurrentDictionary<uint, IOperation> pending)
            : base(clusterController, configInfo, bucketName, pending)
        {
        }

        /// <summary>
        /// Maps a key to a <see cref="IServer"/> object.
        /// </summary>
        /// <param name="key">The key to map.</param>
        /// <returns>The <see cref="IServer"/> where the key lives.</returns>
        private IServer GetServer(string key)
        {
            var keyMapper = ConfigInfo.GetKeyMapper();
            var bucket = keyMapper.MapKey(key);
            return bucket.LocatePrimary();
        }

        /// <summary>
        /// Sends a <see cref="IOperation{T}"/> to the Couchbase Server using the Memcached protocol.
        /// </summary>
        /// <typeparam name="T">The Type of the body of the request.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}"/> to send.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the request.</returns>
        public override IOperationResult<T> SendWithRetry<T>(IOperation<T> operation)
        {
            //Validate key length
            operation.Validate();

            using (Tracer.StartParentScope(operation, ConfigInfo.BucketName))
            {
                IOperationResult<T> operationResult = new OperationResult<T>
                {
                    Success = false, OpCode = operation.OperationCode
                };

                do
                {
                    var server = GetServer(operation.Key);
                    if (server == null)
                    {
                        continue;
                    }
                    operationResult = server.Send(operation);
                    if (operationResult.Success)
                    {
                        Log.Debug("Operation succeeded {0} for key {1}", operation.Attempts, operation.Key);
                        break;
                    }
                    if (operation.CanRetry() && operationResult.ShouldRetry())
                    {
                        var result = operationResult;
                        Log.Debug("Operation retry {0} for key {1}. Reason: {2}", operation.Attempts,
                            operation.Key, result.Message);

                        // Get retry timeout, uses default timeout if no retry stratergy available
                        Thread.Sleep(operation.GetRetryTimeout(VBucketRetrySleepTime));
                    }
                    else
                    {
                        ((OperationResult)operationResult).SetException();
                        Log.Debug("Operation doesn't support retries for key {0}", operation.Key);
                        break;
                    }
                } while (operation.Attempts++ < operation.MaxRetries && !operationResult.Success);

                if (!operationResult.Success)
                {
                    if (operation.TimedOut() && operationResult.ShouldRetry())
                    {
                        const string msg = "The operation has timed out.";
                        ((OperationResult) operationResult).Message = msg;
                        ((OperationResult) operationResult).Status = ResponseStatus.OperationTimeout;
                    }

                    LogFailure(operation, operationResult);
                }

                return operationResult;
            }
        }

        /// <summary>
        /// Sends a <see cref="IOperation{T}"/> to the Couchbase Server using the Memcached protocol.
        /// </summary>
        /// <param name="operation">The <see cref="IOperation{T}"/> to send.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the request.</returns>
        public override IOperationResult SendWithRetry(IOperation operation)
        {
            //Validate key length
            operation.Validate();

            using (Tracer.StartParentScope(operation, ConfigInfo.BucketName))
            {
                IOperationResult operationResult = new OperationResult
                {
                    Success = false,
                    OpCode = operation.OperationCode
                };

                do
                {
                    var server = GetServer(operation.Key);
                    if (server == null)
                    {
                        continue;
                    }
                    operationResult = server.Send(operation);
                    if (operationResult.Success)
                    {
                        Log.Debug("Operation succeeded {0} for key {1}", operation.Attempts, operation.Key);
                        break;
                    }
                    if (operation.CanRetry() && operationResult.ShouldRetry())
                    {
                        var result = operationResult;
                        Log.Debug("Operation retry {0} for key {1}. Reason: {2}", operation.Attempts,
                            operation.Key, result.Message);

                        // Get retry timeout, uses default timeout if no retry stratergy available
                        Thread.Sleep(operation.GetRetryTimeout(VBucketRetrySleepTime));
                    }
                    else
                    {
                        ((OperationResult) operationResult).SetException();
                        Log.Debug("Operation doesn't support retries for key {0}", operation.Key);
                        break;
                    }
                } while (operation.Attempts++ < operation.MaxRetries && !operationResult.Success);

                if (!operationResult.Success)
                {
                    if (operation.TimedOut() && operationResult.ShouldRetry())
                    {
                        const string msg = "The operation has timed out.";
                        ((OperationResult) operationResult).Message = msg;
                        ((OperationResult) operationResult).Status = ResponseStatus.OperationTimeout;
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
                    operation.Completed = CallbackFactory.CompletedFuncWithRetryForMemcached(
                        this, Pending, ClusterController, tcs, cts.Token);

                    Pending.TryAdd(operation.Opaque, operation);

                    IServer server;
                    var attempts = 0;
                    while ((server = GetServer(operation.Key)) == null)
                    {
                        if (attempts++ > 10) { throw new TimeoutException("Could not acquire a server."); }
                        await Task.Delay((int)Math.Pow(2, attempts)).ContinueOnAnyContext();
                    }
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

                return await tcs.Task.ContinueOnAnyContext();
            }
        }

        /// <summary>
        /// Sends a <see cref="IOperation" /> to the Couchbase Server using the Memcached protocol using async/await.
        /// </summary>
        /// <param name="operation">The <see cref="IOperation" /> to send.</param>
        ///  /// <param name="tcs">The <see cref="TaskCompletionSource{T}"/> the represents the task to await on.</param>
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
                    operation.Completed = CallbackFactory.CompletedFuncWithRetryForMemcached(
                        this, Pending, ClusterController, tcs, cts.Token);

                    Pending.TryAdd(operation.Opaque, operation);

                    IServer server;
                    var attempts = 0;
                    while ((server = GetServer(operation.Key)) == null)
                    {
                        if (attempts++ > 10) { throw new TimeoutException("Could not acquire a server."); }
                        await Task.Delay((int)Math.Pow(2, attempts)).ContinueOnAnyContext();
                    }
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

                return await tcs.Task.ContinueOnAnyContext();
            }
        }
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
