using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations;

namespace Couchbase.Core.Retry
{
    internal interface IRetryOrchestrator
    {
        Task<T> RetryAsync<T>(Func<Task<T>> send, IRequest request) where T : IServiceResult;

        Task RetryAsync(BucketBase bucket, IOperation operation, CancellationToken token = default);
    }
}
