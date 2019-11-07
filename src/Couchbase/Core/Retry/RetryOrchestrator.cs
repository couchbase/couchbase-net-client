using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.Retry
{
    public static class RetryOrchestrator
    {
        private static readonly ILogger Log = LogManager.CreateLogger<BucketBase>();

        internal static async Task RetryAsync(this BucketBase bucket, IOperation operation, CancellationToken token = default,
            TimeSpan? timeout = null)
        {
            try
            {
                var backoff = ControlledBackoff.Create();
                do
                {
                    try
                    {
                        operation.Attempts++;
                        await bucket.SendAsync(operation, token, timeout);
                        break;
                    }
                    catch (Exception e)
                    {
                        if (e is IRetryable)
                        {
                            var reason = e.ResolveRetryReason();
                            if (reason.AlwaysRetry())
                            {
                                if (token.IsCancellationRequested) token.ThrowIfCancellationRequested();

                                Log.LogDebug($"Retrying op {operation.Opaque}/{operation.Key} because {reason}.");
                                await backoff.Delay(operation);
                                continue;
                            }

                            var strategy = operation.RetryStrategy;
                            var action = strategy.RetryAfter(operation, reason);

                            if (action.Duration.HasValue)
                            {
                                Log.LogDebug($"Retrying op {operation.Opaque}/{operation.Key} because {reason}.");
                                await Task.Delay(action.Duration.Value, token);
                            }
                            else
                            {
                               break; //don't retry
                            }
                        }
                        else
                        {
                            throw;
                        }
                    }
                } while (true);
            }
            catch (TaskCanceledException){ ThrowTimeoutException(operation, timeout); }
            catch (OperationCanceledException) { ThrowTimeoutException(operation, timeout); }
        }

        public static void ThrowTimeoutException(IOperation operation, TimeSpan? timeout)
        {
            throw new TimeoutException(
                $"The operation {operation.Opaque}/{operation.Opaque} timed out after {timeout}. " +
                $"It was retried {operation.Attempts} times using {operation.RetryStrategy.GetType()}.");
        }
    }
}
