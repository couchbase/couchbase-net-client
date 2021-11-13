using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Collections;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry.Query;
using Couchbase.Core.Sharding;
using Couchbase.KeyValue;
using Couchbase.Utils;
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
            CancellationTokenSource cts1 = null;
            CancellationTokenSource cts2 = null;

            if (request.Timeout > TimeSpan.Zero)
            {
                cts1 = new CancellationTokenSource(request.Timeout);

                if (token.CanBeCanceled)
                {
                    cts2 = CancellationTokenSource.CreateLinkedTokenSource(token, cts1.Token);
                    token = cts2.Token;
                }
                else
                {
                    token = cts1.Token;
                }
            }

            try
            {
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
                                "Retrying query {clientContextId}/{statement} because {reason}.",
                                request.ClientContextId,
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
                                "Retrying query {clientContextId}/{statement} because {reason}.",
                                request.ClientContextId,
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

                                await Task.Delay(cappedDuration, token).ConfigureAwait(false);
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
            finally
            {
                //stop recording metrics and either return result or throw exception
                request.StopRecording();

                cts2?.Dispose();
                cts1?.Dispose();
            }
        }

        public async Task RetryAsync(BucketBase bucket, IOperation operation, CancellationTokenPair tokenPair = default)
        {
            try
            {
                var backoff = ControlledBackoff.Create();
                operation.Token = tokenPair;

                do
                {
                    tokenPair.ThrowIfCancellationRequested();

                    try
                    {
                        operation.Attempts++;

                        try
                        {
                            await bucket.SendAsync(operation, tokenPair).ConfigureAwait(false);
                            break;
                        }
                        catch (CouchbaseException e) when(operation is not GetCid && (e is ScopeNotFoundException || e is CollectionNotFoundException))
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
                    catch (CouchbaseException e) when (e is IRetryable && !tokenPair.IsCancellationRequested)
                    {
                        var reason = e.ResolveRetryReason();
                        if (reason.AlwaysRetry())
                        {
                            _logger.LogDebug("Retrying op {opaque}/{key} because {reason} and always retry.",
                                operation.Opaque,
                                operation.Key, reason);

                            await backoff.Delay(operation).ConfigureAwait(false);

                            // no need to reset op in this case as it was not actually sent
                            if (reason != RetryReason.CircuitBreakerOpen)
                            {
                                operation.Reset();
                            }

                            continue;
                        }

                        var strategy = operation.RetryStrategy;
                        var action = strategy.RetryAfter(operation, reason);

                        if (action.Retry)
                        {
                            _logger.LogDebug("Retrying op {opaque}/{key} because {reason} and action duration.",
                                operation.Opaque,
                                operation.Key, reason);

                            // Reset first so operation is not marked as sent if canceled during the delay
                            operation.Reset();

                            await Task.Delay(action.DurationValue.GetValueOrDefault(), tokenPair)
                                .ConfigureAwait(false);
                        }
                        else
                        {
                            throw; //don't retry
                        }
                    }
                } while (true);
            }
            catch (OperationCanceledException) when (!tokenPair.IsExternalCancellation)
            {
                ThrowHelper.ThrowTimeoutException(operation, new KeyValueErrorContext
                {
                    BucketName = operation.BucketName,
                    ClientContextId = operation.Opaque.ToString(),
                    DocumentKey = operation.Key,
                    Cas = operation.Cas,
                    CollectionName = operation.CName,
                    ScopeName = operation.SName,
                    OpCode = operation.OpCode
                });
            }
        }

        // protected internal for a unit test seam
        protected internal virtual async Task<bool> RefreshCollectionId(BucketBase bucket, IOperation op)
        {
            try
            {
                var scope = await bucket.ScopeAsync(op.SName!).ConfigureAwait(false);

                if (await scope.CollectionAsync(op.CName!).ConfigureAwait(false) is IInternalCollection collection)
                {
                    //re-fetch the CID but do not allow retries in that path and force the CID update
                    await collection.PopulateCidAsync(false, true).ConfigureAwait(false);

                    op.Reset();
                    op.Cid = collection.Cid;

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


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
