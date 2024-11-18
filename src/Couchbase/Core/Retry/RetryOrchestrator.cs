using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry.Query;
using Couchbase.KeyValue;
using Couchbase.Utils;
using Couchbase.Core.IO;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.Retry
{
    internal partial class RetryOrchestrator(
        TimeProvider timeProvider,
        ILogger<RetryOrchestrator> logger,
        TypedRedactor redactor)
        : IRetryOrchestrator
    {
        private readonly ILogger<RetryOrchestrator> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly TypedRedactor _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));

        /// <summary>
        /// Seam for unit testing to change delay behaviors.
        /// </summary>
        internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = timeProvider.Delay;

        public async Task<T> RetryAsync<T>(Func<Task<T>> send, IRequest request) where T : IServiceResult
        {
            var ctsp = CancellationTokenPairSourcePool.Shared.Rent(timeProvider, request.Timeout, request.Token);
            var token = ctsp.Token;

            Type? outcomeErrorType = null;
            try
            {
                //for measuring the capped duration
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var backoff = ControlledBackoff.Create(timeProvider);
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

                            var retryDelay = backoff.CalculateBackoff(request);

                            await Delay(retryDelay, request.Token).ConfigureAwait(false);
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

                                LogCappedQueryDuration(request.ClientContextId, cappedDuration.TotalMilliseconds,
                                    stopwatch.ElapsedMilliseconds);

                                try
                                {
                                    await Delay(cappedDuration, token).ConfigureAwait(false);
                                }
                                catch
                                {

                                }

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
            catch (Exception ex)
            {
                outcomeErrorType = ex.GetType();
                throw;
            }
            finally
            {
                //stop recording metrics and either return result or throw exception
                request.StopRecording(outcomeErrorType);

                CancellationTokenPairSourcePool.Shared.Return(ctsp);
            }
        }

        public async Task<ResponseStatus> RetryAsync(BucketBase bucket, IOperation operation, CancellationTokenPair tokenPair = default)
        {
            Type? outcomeErrorType = null;
            try
            {
                Exception? lastRetriedException = null;

                do
                {
                    if (tokenPair.IsCancellationRequested)
                    {
                        var isExternal = tokenPair.IsExternalCancellation ? "(External)" : string.Empty;
                        var isInternal = tokenPair.IsInternalCancellation ? "(Internal)" : string.Empty;
                        var msg =
                            $"Operation {operation.Opaque}/{_redactor.UserData(operation.Key)} cancelled {isExternal}{isInternal} after {operation.Elapsed.TotalMilliseconds}ms. ({String.Join(",", operation.RetryReasons)})";
                        throw new OperationCanceledException(msg, lastRetriedException, tokenPair.CanceledToken);
                    }

                    try
                    {
                        if (operation.Attempts > 1)
                        {
                            MetricTracker.KeyValue.TrackRetry(operation.OpCode);
                        }

                        var status = await bucket.SendAsync(operation, tokenPair).ConfigureAwait(false);
                        switch (status, operation.OpCode)
                        {
                            // Success cases
                            case (ResponseStatus.Success or ResponseStatus.RangeScanComplete or ResponseStatus.RangeScanMore or ResponseStatus.SubDocSuccessDeletedDocument, _):
                            //For ICouchbaseCollection.ExistsAsync so we do not need to throw and capture the exception
                            case (ResponseStatus.KeyNotFound, OpCode.GetMeta):
                            //sub-doc path failures for lookups are handled when the ContentAs() method is called.
                            //so we simply return back to the caller and let it be handled later.
                            case (ResponseStatus.SubDocMultiPathFailure or ResponseStatus.SubdocMultiPathFailureDeleted, OpCode.MultiLookup):
                            case (ResponseStatus.KeyNotFound, OpCode.RangeScanCreate or OpCode.RangeScanCancel):
                                return status;

                            case (ResponseStatus.RangeScanCanceled, OpCode.RangeScanContinue or OpCode.RangeScanCancel):
                            case (ResponseStatus.UnknownCollection, OpCode.RangeScanContinue or OpCode.RangeScanCreate):
                                // Not sure what this does since we don't throw the exception?
                                status.CreateException(CreateKeyValueErrorContext(bucket, operation, status), operation);
                                break;

                            case (ResponseStatus.UnknownScope or ResponseStatus.UnknownCollection, _):
                                // LogRefreshingCollectionId(e);
                                if (!await RefreshCollectionId(bucket, operation)
                                        .ConfigureAwait(false))
                                {
                                    // rethrow if we fail to refresh the collection ID so we hit retry logic
                                    // otherwise we'll loop and retry immediately
                                    ResetAndIncrementAttempts(operation, status == ResponseStatus.UnknownScope
                                        ? RetryReason.ScopeNotFound
                                        : RetryReason.CollectionNotFound);
                                    continue;
                                }

                                break;

                            case (ResponseStatus.EConfigOnly, _):
                                //If EConfigOnly is returned by the server. We force a config check
                                //to ensure that the config is not stale. See NCBC-3492/CBD-5609
                                await bucket.ForceConfigUpdateAsync().ConfigureAwait(false);
                                break;
                        }

                        if (status.IsRetriable(operation) || operation.RetryNow())
                        {
                            if (await TryPrepareOperationForRetryAndWaitAsync(operation, status.ResolveRetryReason(), tokenPair)
                                    .ConfigureAwait(false))
                            {
                                continue;
                            }
                        }

                        // If we reach this point this is a failure, do not retry
                        if (operation.PreferReturns && ResponseStatus.KeyNotFound == status)
                        {
                            outcomeErrorType = typeof(DocumentNotFoundException);
                            return status;
                        }
                        throw status.CreateException(operation, bucket);
                    }
                    catch (CouchbaseException e) when (e is IRetryable && !tokenPair.IsCancellationRequested)
                    {
                        /*
                         * Note this section is duplicate logic so we can handle the case when an exception is thrown
                         * but the operation may be retried. There are only a few corner cases because most
                         * of the cases are handled above in the try block.
                         */

                        if (!await TryPrepareOperationForRetryAndWaitAsync(operation, e.ResolveRetryReason(), tokenPair)
                                .ConfigureAwait(false))
                        {
                            // Not retrying, rethrow the exception
                            throw;
                        }
                    }
                } while (true);
            }
            catch (OperationCanceledException ex) when (!tokenPair.IsExternalCancellation)
            {
                var errorContext = CreateKeyValueErrorContext(bucket, operation, ResponseStatus.OperationTimeout);

                if (operation.Elapsed < operation.Timeout && !operation.IsCompleted)
                {
                    // Not a true timeout. May execute if an operation is in flight while things are shutting down.
                    outcomeErrorType = typeof(CouchbaseException);
                    ThrowHelper.ThrowFalseTimeoutException(operation, errorContext);
                }

                MetricTracker.KeyValue.TrackTimeout(operation.OpCode);
                var timeoutException = ThrowHelper.CreateTimeoutException(operation, ex, _redactor, errorContext);
                outcomeErrorType = timeoutException.GetType();
                throw timeoutException;
            }
            catch (Exception ex)
            {
                outcomeErrorType = ex.GetType();
                throw;
            }
            finally
            {
                operation.StopRecording(outcomeErrorType);
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
                    if (collection.Cid.HasValue)
                    {
                        op.Cid = collection.Cid;
                    }

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

        // Attempts to prepare an operation to be retried based on the retry reason and strategy. If the operation
        // should not be retried, returns false.
        private async Task<bool> TryPrepareOperationForRetryAndWaitAsync(IOperation operation, RetryReason reason,
            CancellationTokenPair tokenPair)
        {
            var forceRetry = reason.AlwaysRetry();
            var action = forceRetry
                ? new RetryAction(ControlledBackoff.CalculateBackoffCore(operation))
                : operation.RetryStrategy.RetryAfter(operation, reason);
            if (action.Retry || operation.RetryNow())
            {
                if (forceRetry)
                {
                    LogRetryDueToAlwaysRetry(operation.Opaque, _redactor.UserData(operation.Key), reason,
                        operation.ConfigVersion);
                }
                else
                {
                    LogRetryDueToDuration(operation.Opaque, _redactor.UserData(operation.Key), reason);
                }

                // Reset first so operation is not marked as sent if canceled during the delay
                ResetAndIncrementAttempts(operation, reason);

                await Delay(action.DurationValue.GetValueOrDefault(), tokenPair)
                    .ConfigureAwait(false);
                return true;
            }

            // Do not retry
            return false;
        }

        private static void ResetAndIncrementAttempts(IOperation operation, RetryReason reason)
        {
            // Reset first so operation is not marked as sent if canceled during the delay
            // no need to reset op if the circuit breaker is open as it was not actually sent
            if (reason != RetryReason.CircuitBreakerOpen)
            {
                operation.Reset();
            }

            operation.IncrementAttempts(reason);
        }

        private static KeyValueErrorContext CreateKeyValueErrorContext(BucketBase bucket, IOperation operation, ResponseStatus status) =>
            new()
            {
                BucketName = bucket.Name,
                Cas = operation.Cas,
                Status = status,
                ClientContextId = operation.ClientContextId,
                ScopeName = operation.SName,
                CollectionName = operation.CName,
                DispatchedFrom = operation.LastDispatchedFrom,
                DispatchedTo = operation.LastDispatchedTo,
                DocumentKey = operation.Key,
                Message = operation.LastErrorCode?.ToString(),
                OpCode = operation.OpCode,
                RetryReasons = operation.RetryReasons,
            };

        #region Logging

        [LoggerMessage(1, LogLevel.Debug, "Retrying op {opaque}/{key} because {reason} and always retry using configVersion: {configVersion}.")]
        private partial void LogRetryDueToAlwaysRetry(uint opaque, Redacted<string> key, RetryReason reason, ConfigVersion? configVersion);

        [LoggerMessage(2, LogLevel.Debug, "Retrying op {opaque}/{key} because {reason} and action duration.")]
        private partial void LogRetryDueToDuration(uint opaque, Redacted<string> key, RetryReason reason);

        [LoggerMessage(LoggingEvents.QueryEvent, LogLevel.Debug, "Retrying query {clientContextId}/{statement} because {reason}.")]
        private partial void LogRetryQuery(string? clientContextId, Redacted<string?> statement, RetryReason reason);

        [LoggerMessage(100, LogLevel.Information, "Updating stale manifest for collection and retrying.")]
        private partial void LogRefreshingCollectionId(Exception ex);

        [LoggerMessage(101, LogLevel.Error, "Error getting new collection id.")]
        private partial void LogErrorGettingCollectionId(Exception ex);

        [LoggerMessage(200, LogLevel.Debug, "Request was canceled after {elapsed}.")]
        private partial void LogRequestCanceled(long elapsed);

        [LoggerMessage(300, LogLevel.Debug,
            "Timeout for {clientContextId} is {timeout} and duration is {duration} and elapsed is {elapsed}")]
        private partial void LogQueryDurations(string? clientContextId, double timeout, double duration, long elapsed);

        [LoggerMessage(301, LogLevel.Debug, "Timeout for {clientContextId} capped duration is {duration} and elapsed is {elapsed}")]
        private partial void LogCappedQueryDuration(string? clientContextId, double duration, long elapsed);

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
