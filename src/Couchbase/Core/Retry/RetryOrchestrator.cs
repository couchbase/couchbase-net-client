using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.DI;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry.Query;
using Couchbase.Core.Sharding;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.Retry
{
    internal class RetryOrchestrator : IRetryOrchestrator
    {
        private readonly ILogger<RetryOrchestrator> _logger;
        private IRedactor _redactor;

        public RetryOrchestrator(ILogger<RetryOrchestrator> logger, IRedactor redactor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        }

        public async Task<T> RetryAsync<T>(Func<Task<T>> send, IRequest request) where T : IServiceResult
        {
            var token = request.Token;
            if (request.Timeout > TimeSpan.Zero)
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(token, new CancellationTokenSource(request.Timeout).Token);
                token = cts.Token;
            }

            //for measuring the capped duration
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var backoff = ControlledBackoff.Create();
            do
            {
                if (token.IsCancellationRequested)
                {
                    if (request.Idempotent)
                    {
                        UnambiguousTimeoutException.ThrowWithRetryReasons(request);
                    }
                    AmbiguousTimeoutException.ThrowWithRetryReasons(request);
                }

                try
                {
                    var result = await send().ConfigureAwait(false);
                    var reason = result.RetryReason;
                    if (reason == RetryReason.NoRetry) return result;

                    if (reason.AlwaysRetry())
                    {
                        _logger.LogDebug(
                            "Retrying query {clientContextId}/{statement} because {reason}.", request.ClientContextId,
                            _redactor.UserData(request.Statement), reason);

                        await backoff.Delay(request).ConfigureAwait(false);
                        request.IncrementAttempts(reason);
                        continue;
                    }

                    var strategy = request.RetryStrategy;
                    var action = strategy.RetryAfter(request, reason);
                    if (action.Retry)
                    {
                        _logger.LogDebug(LoggingEvents.QueryEvent,
                            "Retrying query {clientContextId}/{statement} because {reason}.", request.ClientContextId,
                            request.Statement, reason);


                        var duration = action.DurationValue;
                        if (duration.HasValue)
                        {
                            _logger.LogDebug(
                                "Timeout for {clientContextId} is {timeout} and duration is {duration} and elapsed is {elapsed}",
                                request.ClientContextId, request.Timeout.TotalMilliseconds,
                                duration.Value.TotalMilliseconds, stopwatch.ElapsedMilliseconds);

                            var cappedDuration =
                                request.Timeout.CappedDuration(duration.Value, stopwatch.Elapsed);

                            _logger.LogDebug(
                                "Timeout for {clientContextId} capped duration is {duration} and elapsed is {elapsed}",
                                request.ClientContextId, cappedDuration.TotalMilliseconds,
                                stopwatch.ElapsedMilliseconds);

                            await Task.Delay(cappedDuration,
                                CancellationTokenSource.CreateLinkedTokenSource(token).Token).ConfigureAwait(false);
                            request.IncrementAttempts(reason);

                            //temp fix for query unit tests
                            if (request.Attempts > 9)
                            {
                                if (request.Idempotent)
                                {
                                    UnambiguousTimeoutException.ThrowWithRetryReasons(request,
                                        new InvalidOperationException($"Too many retries: {request.Attempts}."));
                                }
                                AmbiguousTimeoutException.ThrowWithRetryReasons(request,
                                    new InvalidOperationException($"Too many retries: {request.Attempts}."));
                            }
                        }
                        else
                        {
                            if (request.Idempotent)
                            {
                                UnambiguousTimeoutException.ThrowWithRetryReasons(request);
                            }

                            AmbiguousTimeoutException.ThrowWithRetryReasons(request);
                        }
                    }
                }
                catch (TaskCanceledException _)
                {
                    _logger.LogDebug("Request was canceled after {elapsed}.", stopwatch.ElapsedMilliseconds);
                    //timed out while waiting
                    if (request.Idempotent)
                    {
                        UnambiguousTimeoutException.ThrowWithRetryReasons(request, _);
                    }

                    AmbiguousTimeoutException.ThrowWithRetryReasons(request, _);
                }
            } while (true);
        }

        public async Task RetryAsync(BucketBase bucket, IOperation operation, CancellationToken token = default)
        {
            try
            {
                var backoff = ControlledBackoff.Create();
                do
                {
                    try
                    {
                        operation.Attempts++;

                        try
                        {
                            await bucket.SendAsync(operation, token).ConfigureAwait(false);
                            break;
                        }
                        catch (CollectionOutdatedException e)
                        {
                            // We catch CollectionOutdatedException separately from the CouchbaseException catch block
                            // in case RefreshCollectionId fails. This causes that failure to trigger normal retry logic.

                            _logger.LogInformation("Updating stale manifest for collection and retrying.", e);
                            if (!await RefreshCollectionId(bucket, operation)
                                .ConfigureAwait(false))
                            {
                                // rethrow if we fail to refresh he collection ID so we hit retry logic
                                // otherwise we'll loop and retry immediately
                                throw;
                            }
                        }
                    }
                    catch (CouchbaseException e)
                    {
                        if (e is IRetryable)
                        {
                            var reason = e.ResolveRetryReason();
                            if (reason.AlwaysRetry())
                            {
                                token.ThrowIfCancellationRequested();

                                _logger.LogDebug("Retrying op {opaque}/{key} because {reason} and always retry.",
                                    operation.Opaque,
                                    operation.Key, reason);

                                await backoff.Delay(operation).ConfigureAwait(false);
                                operation.Reset();
                                continue;
                            }

                            var strategy = operation.RetryStrategy;
                            var action = strategy.RetryAfter(operation, reason);

                            if (action.Retry)
                            {
                                _logger.LogDebug("Retrying op {opaque}/{key} because {reason} and action duration.",
                                    operation.Opaque,
                                    operation.Key, reason);

                                operation.Reset();
                                await Task.Delay(action.DurationValue.Value, token).ConfigureAwait(false);
                            }
                            else
                            {
                                throw; //don't retry
                            }
                        }
                        else
                        {
                            throw;
                        }
                    }
                } while (true);
            }
            catch (OperationCanceledException) { ThrowTimeoutException(operation, operation.Timeout); }
        }

        private static void ThrowTimeoutException(IOperation operation, TimeSpan? timeout)
        {
            throw new TimeoutException(
                $"The operation {operation.Opaque}/{operation.Opaque} timed out after {timeout}. " +
                $"It was retried {operation.Attempts} times using {operation.RetryStrategy.GetType()}.");
        }

        // protected internal for a unit test seam
        protected internal virtual async Task<bool> RefreshCollectionId(BucketBase bucket, IOperation op)
        {
            try
            {
                var vBucket = (VBucket) bucket.KeyMapper!.MapKey(op.Key);
                var endPoint = op.ReplicaIdx > 0 ? vBucket.LocateReplica(op.ReplicaIdx) : vBucket.LocatePrimary();

                if (bucket.Nodes.TryGet(endPoint!, out var clusterNode))
                {
                    var scope = bucket.Scope(op.SName);
                    var collection = (CouchbaseCollection) scope.Collection(op.CName);

                    var newCid = await clusterNode.GetCid($"{op.SName}.{op.CName}").ConfigureAwait(false);
                    collection.Cid = newCid;

                    op.Reset();
                    op.Cid = newCid;

                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting new collection id.");
            }

            op.Reset();
            return false;
        }
    }
}
