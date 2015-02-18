using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Configuration;
using Couchbase.Core.Diagnostics;
using Couchbase.IO;
using Couchbase.IO.Operations;

namespace Couchbase.Core.Buckets
{
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
        /// An <see cref="Task{IOperationResult}" /> object representing the asynchronous operation.
        public virtual async Task<IOperationResult<T>> RetryOperationEveryAsync<T>(
            Func<IOperation<T>, IConfigInfo, Task<IOperationResult<T>>> execute,
            IOperation<T> operation,
            IConfigInfo configInfo,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                var result = await execute(operation, configInfo).ConfigureAwait(false);
                if (result.Success || operation.TimedOut() || !operation.CanRetry() || result.Status != ResponseStatus.VBucketBelongsToAnotherServer)
                {
                    if (operation.TimedOut())
                    {
                        const string msg = "The operation has timed out. Retried [{0}] times.";
                        ((OperationResult)result).Message = string.Format(msg, operation.Attempts);
                        ((OperationResult)result).Status = ResponseStatus.OperationTimeout;
                    }
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

        public virtual IOperationResult<T> SendWithRetry<T>(IOperation<T> operation)
        {
            throw new NotImplementedException();
        }

        public virtual Task<IOperationResult<T>> SendWithRetryAsync<T>(IOperation<T> operation)
        {
            throw new NotImplementedException();
        }

        public virtual Views.IViewResult<T> SendWithRetry<T>(Views.IViewQuery query)
        {
            throw new NotImplementedException();
        }

        public virtual Task<Views.IViewResult<T>> SendWithRetryAsync<T>(Views.IViewQuery query)
        {
            throw new NotImplementedException();
        }

        public virtual N1QL.IQueryResult<T> SendWithRetry<T>(N1QL.IQueryRequest queryRequest)
        {
            throw new NotImplementedException();
        }

        public virtual Task<N1QL.IQueryResult<T>> SendWithRetryAsync<T>(N1QL.IQueryRequest queryRequest)
        {
            throw new NotImplementedException();
        }

        public virtual IOperationResult<T> SendWithDurability<T>(IOperation<T> operation, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotImplementedException();
        }

        public virtual Task<IOperationResult<T>> SendWithDurabilityAsync<T>(IOperation<T> operation, bool deletion, ReplicateTo replicateTo, PersistTo persistTo)
        {
            throw new NotImplementedException();
        }
    }
}