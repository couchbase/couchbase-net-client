using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Configuration;
using Couchbase.IO;
using Couchbase.IO.Operations;

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
        IServer GetServer(string key)
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
            IOperationResult<T> operationResult = new OperationResult<T> { Success = false };
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
                    Log.Debug(m => m("Operation succeeded {0} for key {1}", operation.Attempts, operation.Key));
                    break;
                }
                if (CanRetryOperation(operationResult, operation, server))
                {
                    Log.Debug(m => m("Operation retry {0} for key {1}. Reason: {2}", operation.Attempts,
                    operation.Key, operationResult.Message));
                }
                else
                {
                    Log.Debug(m => m("Operation doesn't support retries for key {0}", operation.Key));
                    break;
                }
            } while (operation.Attempts++ < operation.MaxRetries && !operationResult.Success);

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
            return operationResult;
        }

        /// <summary>
        /// Sends a <see cref="IOperation{T}"/> to the Couchbase Server using the Memcached protocol.
        /// </summary>
        /// <typeparam name="T">The Type of the body of the request.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}"/> to send.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the request.</returns>
        public override IOperationResult SendWithRetry(IOperation operation)
        {
            IOperationResult operationResult = new OperationResult { Success = false };
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
                    Log.Debug(m => m("Operation succeeded {0} for key {1}", operation.Attempts, operation.Key));
                    break;
                }
                if (CanRetryOperation(operationResult, operation, server))
                {
                    Log.Debug(m => m("Operation retry {0} for key {1}. Reason: {2}", operation.Attempts,
                    operation.Key, operationResult.Message));
                }
                else
                {
                    Log.Debug(m => m("Operation doesn't support retries for key {0}", operation.Key));
                    break;
                }
            } while (operation.Attempts++ < operation.MaxRetries && !operationResult.Success);

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
            return operationResult;
        }

        public  bool OperationSupportsRetries(IOperation operation)
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
            return supportsRetry;
        }

        //TODO this logic is partly legacy and will refactored in a subsequent commit before 2.1.0
        public bool CanRetryOperation(IOperationResult operationResult, IOperation operation, IServer server)
        {
            var supportsRetry = OperationSupportsRetries(operation);
            var retry = false;
            switch (operationResult.Status)
            {
                case ResponseStatus.Success:
                case ResponseStatus.KeyNotFound:
                case ResponseStatus.KeyExists:
                case ResponseStatus.ValueTooLarge:
                case ResponseStatus.InvalidArguments:
                case ResponseStatus.ItemNotStored:
                case ResponseStatus.IncrDecrOnNonNumericValue:
                case ResponseStatus.VBucketBelongsToAnotherServer:
                    break;
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
                case ResponseStatus.ClientFailure:
                    retry = supportsRetry;
                    break;
            }
            return retry;
        }

        /// <summary>
        /// Sends a <see cref="IOperation{T}" /> to the Couchbase Server using the Memcached protocol using async/await.
        /// </summary>
        /// <typeparam name="T">The Type of the body of the request.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}" /> to send.</param>
        /// <returns>
        /// An <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public override Task<IOperationResult<T>> SendWithRetryAsync<T>(IOperation<T> operation)
        {
            var tcs = new TaskCompletionSource<IOperationResult<T>>();
            var cts = new CancellationTokenSource(OperationLifeSpan);
            cts.CancelAfter(OperationLifeSpan);

            operation.Completed = CallbackFactory.CompletedFuncWithRetryForMemcached(this, Pending, ClusterController, tcs, cts.Token);
            Pending.TryAdd(operation.Opaque, operation);

            var server = GetServer(operation.Key);
            server.SendAsync(operation).ConfigureAwait(false);
            return tcs.Task;
        }

        /// <summary>
        /// Sends a <see cref="IOperation" /> to the Couchbase Server using the Memcached protocol using async/await.
        /// </summary>
        /// <param name="operation">The <see cref="IOperation" /> to send.</param>
        /// <returns>
        /// An <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        /// </returns>
        public override Task<IOperationResult> SendWithRetryAsync(IOperation operation)
        {
            var tcs = new TaskCompletionSource<IOperationResult>();
            var cts = new CancellationTokenSource(OperationLifeSpan);
            cts.CancelAfter(OperationLifeSpan);

            operation.Completed = CallbackFactory.CompletedFuncWithRetryForMemcached(this, Pending, ClusterController, tcs, cts.Token);
            Pending.TryAdd(operation.Opaque, operation);

            var server = GetServer(operation.Key);
            server.SendAsync(operation).ConfigureAwait(false);
            return tcs.Task;
        }
    }
}
