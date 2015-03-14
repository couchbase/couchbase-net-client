using System;
using Common.Logging;
using Couchbase.Configuration;
using Couchbase.Core.Diagnostics;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;

namespace Couchbase.Core.Buckets
{
    /// <summary>
    /// An implementation of <see cref="IRequestExecuter"/> for executing memcached specific operations against an
    /// in-memory, Memcached Bucket in a Couchbase cluster.
    /// </summary>
    /// <remarks>Note that the only methods which Memcached buckets support are implemented.
    /// Methods that are not implemented may throw a <see cref="NotSupportedException"/>.</remarks>
    internal class MemcachedRequestExecuter : IRequestExecuter
    {
        private static readonly ILog Log = LogManager.GetLogger<CouchbaseRequestExecuter>();
        private readonly IClusterController _clusterController;
        private readonly IConfigInfo _configInfo;
        private readonly IByteConverter _converter;
        private readonly string _bucketName;
        private const uint operationLifeSpan = 2500;
        private volatile bool _timingEnabled;
        private readonly Func<TimingLevel, object, IOperationTimer> Timer;

        public MemcachedRequestExecuter(IClusterController clusterController, IConfigInfo configInfo, IByteConverter converter, string bucketName)
        {
            _clusterController = clusterController;
            _configInfo = configInfo;
            _converter = converter;
            _bucketName = bucketName;
            Timer = _clusterController.Configuration.Timer;
            _timingEnabled = _clusterController.Configuration.EnableOperationTiming;

        }

        /// <summary>
        /// Maps a key to a <see cref="IServer"/> object.
        /// </summary>
        /// <param name="key">The key to map.</param>
        /// <returns>The <see cref="IServer"/> where the key lives.</returns>
        IServer GetServer(string key)
        {
            var keyMapper = _configInfo.GetKeyMapper();
            var bucket = keyMapper.MapKey(key);
            return bucket.LocatePrimary();
        }

        /// <summary>
        /// Sends a <see cref="IOperation{T}"/> to the Couchbase Server using the Memcached protocol.
        /// </summary>
        /// <typeparam name="T">The Type of the body of the request.</typeparam>
        /// <param name="operation">The <see cref="IOperation{T}"/> to send.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the request.</returns>
        public IOperationResult<T> SendWithRetry<T>(IOperation<T> operation)
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

        public  bool OperationSupportsRetries<T>(IOperation<T> operation)
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
        public bool CanRetryOperation<T>(IOperationResult<T> operationResult, IOperation<T> operation, IServer server)
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

        public System.Threading.Tasks.Task<IOperationResult<T>> SendWithRetryAsync<T>(IOperation<T> operation)
        {
            throw new NotImplementedException();
        }

        public Views.IViewResult<T> SendWithRetry<T>(Views.IViewQuery query)
        {
            throw new NotSupportedException("This method is not supported by Memcached buckets.");
        }

        public System.Threading.Tasks.Task<Views.IViewResult<T>> SendWithRetryAsync<T>(Views.IViewQuery query)
        {
            throw new NotSupportedException("This method is not supported by Memcached buckets.");
        }

        public N1QL.IQueryResult<T> SendWithRetry<T>(N1QL.IQueryRequest queryRequest)
        {
            throw new NotSupportedException("This method is not supported by Memcached buckets.");
        }

        public System.Threading.Tasks.Task<N1QL.IQueryResult<T>> SendWithRetryAsync<T>(N1QL.IQueryRequest queryRequest)
        {
            throw new NotSupportedException("This method is not supported by Memcached buckets.");
        }

        public IOperationResult<T> SendWithDurability<T>(IOperation<T> operation, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is not supported by Memcached buckets.");
        }

        public System.Threading.Tasks.Task<IOperationResult<T>> SendWithDurabilityAsync<T>(IOperation<T> operation, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotSupportedException("This method is not supported by Memcached buckets.");
        }
    }
}
