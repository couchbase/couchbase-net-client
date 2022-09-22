using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Collections;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry.Query;
using Couchbase.Core.Utils;
using Couchbase.KeyValue;
using Couchbase.Utils;
using Couchbase.Core.IO;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.Retry
{
    internal partial class RetryOrchestrator : IRetryOrchestrator
    {
        private readonly ILogger<RetryOrchestrator> _logger;
        private readonly TypedRedactor _redactor;

        public RetryOrchestrator(ILogger<RetryOrchestrator> logger, TypedRedactor redactor)
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
                cts1 = CancellationTokenSourcePool.Shared.Rent(request.Timeout);

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
                            LogRetryQuery(request.ClientContextId, _redactor.UserData(request.Statement), reason);

                            await backoff.Delay(request).ConfigureAwait(false);
                            request.IncrementAttempts(reason);
                            continue;
                        }

                        var strategy = request.RetryStrategy;
                        var action = strategy.RetryAfter(request, reason);
                        if (action.Retry)
                        {
                            LogRetryQuery(request.ClientContextId, _redactor.UserData(request.Statement), reason);

                            var duration = action.DurationValue;
                            if (duration.HasValue)
                            {
                                LogQueryDurations(request.ClientContextId, request.Timeout.TotalMilliseconds,
                                    duration.Value.TotalMilliseconds, stopwatch.ElapsedMilliseconds);

                                var cappedDuration =
                                    request.Timeout.CappedDuration(duration.Value, stopwatch.Elapsed);

                                LogCappedQueryDuration(request.ClientContextId, cappedDuration.TotalMilliseconds, stopwatch.ElapsedMilliseconds);

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
                        else
                        {
                            //don't retry
                            result.ThrowOnNoRetry();
                        }
                    }
                    catch (TaskCanceledException _)
                    {
                        LogRequestCanceled(stopwatch.ElapsedMilliseconds);

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

                if (cts1 is not null)
                {
                    CancellationTokenSourcePool.Shared.Return(cts1);
                }
            }
        }

        public async Task<ResponseStatus> RetryAsync(BucketBase bucket, IOperation operation, CancellationTokenPair tokenPair = default)
        {
            try
            {
                var backoff = ControlledBackoff.Create();
                Exception lastRetriedException = null;

                do
                {
                    if (tokenPair.IsCancellationRequested)
                    {
                        var isExternal = tokenPair.IsExternalCancellation ? "(External)" : string.Empty;
                        var isInternal = tokenPair.IsInternalCancellation ? "(Internal)" : string.Empty;
                        var msg = $"Operation {operation.Opaque}/{_redactor.UserData(operation.Key)} cancelled {isExternal}{isInternal} after {operation.Elapsed.TotalMilliseconds}ms. ({String.Join(",", operation.RetryReasons)})";
                        throw new OperationCanceledException(msg, lastRetriedException, tokenPair.CanceledToken);
                    }

                    try
                    {
                        if (operation.Attempts > 1)
                        {
                            MetricTracker.KeyValue.TrackRetry(operation.OpCode);
                        }

                        var status = await bucket.SendAsync(operation, tokenPair).ConfigureAwait(false);
                        if (status == ResponseStatus.Success)
                        {
                            return status;
                        }

                        //For ICouchbaseCollection.ExistsAsync so we do not need to throw and capture the exception
                        if (status == ResponseStatus.KeyNotFound && operation.OpCode == OpCode.GetMeta)
                        {
                            return status;
                        }
                        if (status.IsRetriable(operation))
                        {
                            if (status == ResponseStatus.UnknownScope || status == ResponseStatus.UnknownCollection)
                            {
                                // LogRefreshingCollectionId(e);
                                if (!await RefreshCollectionId(bucket, operation)
                                        .ConfigureAwait(false))
                                {
                                    // rethrow if we fail to refresh the collection ID so we hit retry logic
                                    // otherwise we'll loop and retry immediately
                                    operation.Reset();
                                    operation.IncrementAttempts(status == ResponseStatus.UnknownScope ?
                                        RetryReason.ScopeNotFound :
                                        RetryReason.CollectionNotFound);
                                    continue;
                                }
                            }

                            var reason = status.ResolveRetryReason();
                            if (reason.AlwaysRetry())
                            {
                                LogRetryDueToAlwaysRetry(operation.Opaque, _redactor.UserData(operation.Key), reason);

                                await backoff.Delay(operation).ConfigureAwait(false);

                                // no need to reset op in this case as it was not actually sent
                                if (reason != RetryReason.CircuitBreakerOpen)
                                {
                                    operation.Reset();
                                }
                                operation.IncrementAttempts(reason);
                                continue;
                            }
                            var strategy = operation.RetryStrategy;
                            var action = strategy.RetryAfter(operation, reason);

                            if (action.Retry)
                            {
                                LogRetryDueToDuration(operation.Opaque, _redactor.UserData(operation.Key), reason);

                                // Reset first so operation is not marked as sent if canceled during the delay
                                operation.Reset();
                                operation.IncrementAttempts(reason);

                                await Task.Delay(action.DurationValue.GetValueOrDefault(), tokenPair)
                                    .ConfigureAwait(false);
                            }
                            else
                            {
                                throw status.CreateException(operation, bucket);
                            }
                        }
                        else
                        {
                            //do not retry just raise the exception
                            throw status.CreateException(operation, bucket);
                        }
                    }
                    catch (CouchbaseException e) when (e is IRetryable && !tokenPair.IsCancellationRequested)
                    {
                        /*
                         * Note this section is duplicate logic so we can the case when an exception is thrown
                         * but the operation may be retried. There are only a few corner cases because most all
                         * of the cases are handled above in the try block.
                         */
                        var reason = e.ResolveRetryReason();
                        if (reason.AlwaysRetry())
                        {
                            lastRetriedException = e;
                            LogRetryDueToAlwaysRetry(operation.Opaque, _redactor.UserData(operation.Key), reason);

                            try
                            {
                                await backoff.Delay(operation).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                            { }

                            // no need to reset op in this case as it was not actually sent
                            if (reason != RetryReason.CircuitBreakerOpen)
                            {
                                operation.Reset();
                            }
                            operation.IncrementAttempts(reason);
                            continue;
                        }
                        var strategy = operation.RetryStrategy;
                        var action = strategy.RetryAfter(operation, reason);
                        if (action.Retry)
                        {
                            lastRetriedException = e;
                            LogRetryDueToDuration(operation.Opaque, _redactor.UserData(operation.Key), reason);
                            // Reset first so operation is not marked as sent if canceled during the delay
                            operation.Reset();
                            operation.IncrementAttempts(reason);

                            try
                            {
                                await backoff.Delay(operation).ConfigureAwait(false);
                            }
                            catch (OperationCanceledException)
                            { }
                        }
                        else
                        {
                            throw; //don't retry
                        }
                    }
                } while (true);
            }
            catch (OperationCanceledException ex) when (!tokenPair.IsExternalCancellation)
            {
                operation.StopRecording();

                if (operation.Elapsed < operation.Timeout)
                {
                    // not a true timeout.
                    throw;
                }

                MetricTracker.KeyValue.TrackTimeout(operation.OpCode);
                ThrowHelper.ThrowTimeoutException(operation, ex, _redactor, new KeyValueErrorContext
                {
                    BucketName = operation.BucketName,
                    ClientContextId = operation.Opaque.ToStringInvariant(),
                    DocumentKey = operation.Key,
                    Cas = operation.Cas,
                    Status = ResponseStatus.OperationTimeout,
                    CollectionName = operation.CName,
                    ScopeName = operation.SName,
                    OpCode = operation.OpCode,
                    DispatchedFrom = operation.LastDispatchedFrom,
                    DispatchedTo = operation.LastDispatchedTo,
                    RetryReasons = operation.RetryReasons
                });
            }

            return ResponseStatus.Failure;//what to do here?
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
                LogErrorGettingCollectionId(ex);
            }

            op.Reset();
            return false;
        }

        #region Logging

        [LoggerMessage(1, LogLevel.Debug, "Retrying op {opaque}/{key} because {reason} and always retry.")]
        private partial void LogRetryDueToAlwaysRetry(uint opaque, Redacted<string> key, RetryReason reason);

        [LoggerMessage(2, LogLevel.Debug, "Retrying op {opaque}/{key} because {reason} and action duration.")]
        private partial void LogRetryDueToDuration(uint opaque, Redacted<string> key, RetryReason reason);

        [LoggerMessage(LoggingEvents.QueryEvent, LogLevel.Debug, "Retrying query {clientContextId}/{statement} because {reason}.")]
        private partial void LogRetryQuery(string clientContextId, Redacted<string> statement, RetryReason reason);

        [LoggerMessage(100, LogLevel.Information, "Updating stale manifest for collection and retrying.")]
        private partial void LogRefreshingCollectionId(Exception ex);

        [LoggerMessage(101, LogLevel.Error, "Error getting new collection id.")]
        private partial void LogErrorGettingCollectionId(Exception ex);

        [LoggerMessage(200, LogLevel.Debug, "Request was canceled after {elapsed}.")]
        private partial void LogRequestCanceled(long elapsed);

        [LoggerMessage(300, LogLevel.Debug,
            "Timeout for {clientContextId} is {timeout} and duration is {duration} and elapsed is {elapsed}")]
        private partial void LogQueryDurations(string clientContextId, double timeout, double duration, long elapsed);

        [LoggerMessage(301, LogLevel.Debug, "Timeout for {clientContextId} capped duration is {duration} and elapsed is {elapsed}")]
        private partial void LogCappedQueryDuration(string clientContextId, double duration, long elapsed);

        #endregion
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
