using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Retry;
using Couchbase.Management.Buckets;
using Couchbase.Management.Collections;
using Google.Protobuf.WellKnownTypes;
using Google.Rpc;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using BucketNotFoundException = Couchbase.Core.Exceptions.BucketNotFoundException;
using CollectionNotFoundException = Couchbase.Core.Exceptions.CollectionNotFoundException;

#nullable enable

namespace Couchbase.Stellar.Core.Retry;

internal partial class StellarRetryHandler : IRetryOrchestrator
{
    /// <summary>
    /// The smallest grace an attempt gets past its gRPC deadline before we stop waiting for it.
    /// </summary>
    internal static readonly TimeSpan MinimumAttemptGrace = TimeSpan.FromMilliseconds(100);

    private readonly TimeProvider _timeProvider;
    private readonly ILogger<StellarRetryHandler> _logger;

    public StellarRetryHandler() : this(TimeProvider.System) { }

    /// <summary>
    /// Creates a StellarRetryHandler with a custom TimeProvider for deterministic testing.
    /// </summary>
    internal StellarRetryHandler(TimeProvider timeProvider)
        : this(timeProvider, NullLogger<StellarRetryHandler>.Instance)
    {
    }

    internal StellarRetryHandler(TimeProvider timeProvider, ILogger<StellarRetryHandler> logger)
    {
        _timeProvider = timeProvider;
        _logger = logger;
        Delay = _timeProvider.Delay;
    }

    /// <summary>
    /// The backoff wait between retry attempts, as a delegate so tests can substitute it — the same seam
    /// <see cref="Couchbase.Core.Retry.RetryOrchestrator"/> exposes. Tests advance an injected
    /// <see cref="TimeProvider"/> by the backoff and return synchronously, which keeps the timeout budget
    /// honest without leaving a real timer for the test to wait on.
    /// </summary>
    internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; }

    public async Task<T> RetryAsync<T>(Func<Task<T>> send, IRequest request) where T : IServiceResult
    {
        var backoff = ControlledBackoff.Create(_timeProvider);
        var context = new GenericErrorContext();
        System.Type? finalErrorType = null;

        try
        {
            while (true)
            {
                var remaining = (request as StellarRequest)?.RemainingTimeout;
                if (request.Token.IsCancellationRequested || remaining <= TimeSpan.Zero)
                {
                    // An expired budget gives up here rather than sending one more call with a
                    // deadline already in the past.
                    throw CreateTimeoutException(request, context, "The request timed out.");
                }

                try
                {
                    var attempt = send();
                    return remaining is { } budget
                        ? await WaitForAttemptAsync(attempt, budget + AttemptGrace(request.Timeout), request, context)
                            .ConfigureAwait(false)
                        : await attempt.ConfigureAwait(false);
                }
                catch (RpcException e)
                {
                    if (e.StatusCode != StatusCode.OK)
                    {
                        HandleException(e, request, context);
                        await Delay(backoff.CalculateBackoff(request), request.Token).ConfigureAwait(false);
                    }
                }
                catch (Exception e) when (IsTransientTransportException(e))
                {
                    // HTTP/2 transport failures (e.g. connection reset, broken pipe) can
                    // surface as HttpRequestException or IOException rather than RpcException.
                    // Treat these like Unavailable and retry.
                    context.RetryReasons.Add(RetryReason.ServiceNotAvailable);
                    request.Attempts++;
                    await Delay(backoff.CalculateBackoff(request), request.Token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex)
        {
            finalErrorType = ex.GetType();
            throw;
        }
        finally
        {
            if (request is StellarRequest stellarRequest)
                stellarRequest.StopRecording(finalErrorType);
            else
                request.StopRecording();
        }
    }

    /// <summary>
    /// Maps an error raised mid-stream — after the first response was returned — while reading a
    /// streaming result, returning the exception the caller should throw. Per RFC 77 a mid-stream
    /// error cannot be retried (rows may already have been delivered): retryable and transport errors
    /// map to <see cref="RequestCanceledException"/>, terminal errors to their mapped SDK exception,
    /// and anything we don't recognise is returned unchanged for the caller to rethrow.
    /// </summary>
    internal Exception MapMidStreamException(Exception e, IRequest request)
    {
        var context = new GenericErrorContext();

        if (e is RpcException rpc && rpc.StatusCode != StatusCode.OK)
        {
            try
            {
                // HandleException returns after recording a retry reason for retryable codes, and
                // throws the mapped SDK exception for terminal ones — capture it to return.
                HandleException(rpc, request, context);
            }
            catch (Exception mapped)
            {
                return mapped;
            }
        }
        else if (IsTransientTransportException(e))
        {
            // Transport failure outside an RpcException (e.g. connection reset) — retryable.
            context.RetryReasons.Add(RetryReason.ServiceNotAvailable);
        }
        else
        {
            // Not something we map — return it unchanged for the caller to rethrow.
            return e;
        }

        // Retryable: carry the SDK error context. Per RFC 77 we don't nest the raw gRPC
        // exception (TODO CNG-4: put the cause in lastException/lastError).
        return new RequestCanceledException(
            "A retryable error occurred mid-stream; the request was cancelled and cannot be retried.")
        {
            Context = context
        };
    }

    /// <summary>
    /// How long past its gRPC deadline we keep waiting for an attempt: 10% of the operation's timeout,
    /// and at least <see cref="MinimumAttemptGrace"/>. It lets gRPC's own DeadlineExceeded win in the
    /// normal case, so the errors callers see do not change.
    /// </summary>
    internal static TimeSpan AttemptGrace(TimeSpan timeout)
    {
        var tenth = TimeSpan.FromTicks(timeout.Ticks / 10);
        return tenth > MinimumAttemptGrace ? tenth : MinimumAttemptGrace;
    }

    /// <summary>
    /// Waits for one attempt, but never longer than <paramref name="limit"/>. The gRPC deadline should
    /// end the call first; this is the backstop for a call that gRPC never completes, which otherwise
    /// left the operation waiting forever despite its timeout (NCBC-4318). The caller's token is not
    /// passed here: gRPC already observes it, and turns it into the RequestCanceledException callers
    /// expect.
    /// </summary>
    private async Task<T> WaitForAttemptAsync<T>(Task<T> attempt, TimeSpan limit, IRequest request,
        GenericErrorContext context)
    {
        try
        {
            return await attempt.WaitAsync(limit, _timeProvider, CancellationToken.None).ConfigureAwait(false);
        }
        catch (System.TimeoutException) when (!attempt.IsCompleted)
        {
            // Nobody will await the abandoned call, so observe its exception if it ever faults.
            _ = attempt.ContinueWith(static t => _ = t.Exception, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);

            LogAttemptDidNotComplete(limit, request.Attempts);
            throw CreateTimeoutException(request, context,
                "The request timed out: the gRPC call did not complete, even after its deadline.");
        }
    }

    private static CouchbaseException CreateTimeoutException(IRequest request, GenericErrorContext context,
        string message) =>
        IsReadOnly(request)
            ? new UnambiguousTimeoutException(message, context)
            : new AmbiguousTimeoutException(message, context);

    [LoggerMessage(1, LogLevel.Warning,
        "A gRPC call did not complete within {Limit} (its deadline plus grace), so the operation gave up after {Attempts} retries.")]
    private partial void LogAttemptDidNotComplete(TimeSpan limit, uint attempts);

    // Timeout ambiguity keys on whether the op mutates server state, not on idempotency: an
    // idempotent-but-mutating op (GetAndLock, MutateIn, ...) is safe to retry yet may have applied
    // on timeout, so it must surface as Ambiguous. Fall back to Idempotent for non-StellarRequest.
    private static bool IsReadOnly(IRequest request) =>
        (request as StellarRequest)?.ReadOnly ?? request.Idempotent;

    private void HandleException(RpcException protoException, IRequest request, GenericErrorContext context)
    {
        var status = protoException.StatusCode;
        var detail = protoException.Status.Detail;
        var detailBlock = StatusDeserializer(protoException);
        var typeUrl = detailBlock?.TypeUrl;

        if (detailBlock is not null)
        {
            switch (typeUrl)
            {
                case StellarRetryStrings.TypeUrlResourceInfo:
                {
                    ResourceInfo info = ResourceInfo.Parser.ParseFrom(detailBlock?.Value);
                    var resourceName = info.ResourceName;
                    var resourceType = info.ResourceType;

                    context.Fields.Add("Detail", detail);
                    context.Fields.Add("ResourceName", resourceName);
                    context.Fields.Add("ResourceType", resourceType);

                    switch (status)
                    {
                        case StatusCode.NotFound:
                        {
                            switch (resourceType)
                            {
                                case StellarRetryStrings.ResourceTypeDocument:
                                    throw new DocumentNotFoundException(context);
                                case StellarRetryStrings.ResourceTypeBucket:
                                    throw new BucketNotFoundException(context);
                                case StellarRetryStrings.ResourceTypeScope:
                                    throw new ScopeNotFoundException(context);
                                case StellarRetryStrings.ResourceTypeCollection:
                                    throw new CollectionNotFoundException(context);
                                case StellarRetryStrings.ResourceTypePath:
                                    throw new PathNotFoundException(context);
                                case StellarRetryStrings.ResourceTypeAnalyticsIndex:
                                case StellarRetryStrings.ResourceTypeQueryIndex:
                                case StellarRetryStrings.ResourceTypeSearchIndex:
                                    throw new IndexNotFoundException(context);
                            }

                            break;
                        }
                        case StatusCode.AlreadyExists:
                        {
                            switch (resourceType)
                            {
                                case StellarRetryStrings.ResourceTypeDocument:
                                    throw new DocumentExistsException(context);
                                case StellarRetryStrings.ResourceTypeBucket:
                                    throw new BucketExistsException(resourceName);
                                case StellarRetryStrings.ResourceTypeScope:
                                    throw new ScopeExistsException(resourceName);
                                case StellarRetryStrings.ResourceTypeCollection:
                                    throw new CollectionExistsException(
                                        $"The collection at {resourceName} already exists.");
                                case StellarRetryStrings.ResourceTypePath:
                                    throw new PathExistsException(context);
                                case StellarRetryStrings.ResourceTypeAnalyticsIndex:
                                case StellarRetryStrings.ResourceTypeQueryIndex:
                                case StellarRetryStrings.ResourceTypeSearchIndex:
                                    throw new IndexExistsException(context);
                            }

                            break;
                        }
                    }
                }
                    break;
                case StellarRetryStrings.TypeUrlErrorInfo:
                {
                    var errorInfo = ErrorInfo.Parser.ParseFrom(detailBlock.Value);
                    var reason = errorInfo.Reason;
                    context.Fields.Add("ErrorReason", reason);

                    if (status == StatusCode.Aborted)
                    {
                        if (reason.Equals(StellarRetryStrings.ReasonCasMismatch))
                        {
                            throw new CasMismatchException(context);
                        }
                    }
                }
                    break;
                case StellarRetryStrings.TypeUrlPreconditionFailure:
                {
                    var info = PreconditionFailure.Parser.ParseFrom(detailBlock.Value);
                    if (info.Violations.Count > 0)
                    {
                        var violation = info.Violations[0];
                        var type = violation.Type;

                        switch (type)
                        {
                            case StellarRetryStrings.PreconditionLocked:
                            {
                                // Retryable per RFC 77 (KV_LOCKED). return (not break) so we retry with
                                // backoff rather than falling through to the status switch.
                                context.RetryReasons.Add(RetryReason.KvLocked);
                                request.Attempts++;
                                return;
                            }
                            case StellarRetryStrings.Unlocked:
                                throw new DocumentNotLockedException();
                            case StellarRetryStrings.PreconditionPathMismatch:
                                throw new PathMismatchException(context);
                            case StellarRetryStrings.PreconditionDocNotJson:
                                throw new DocumentNotJsonException(context);
                            case StellarRetryStrings.PreconditionDocTooDeep:
                                throw new DocumentTooDeepException(context);
                            case StellarRetryStrings.PreconditionValueTooLarge:
                                throw new ValueToolargeException();
                            case StellarRetryStrings.PreconditionValueOutOfRange:
                                throw
                                    new ValueInvalidException(); //Prone to change as it's unclear what this Precondition failure maps to.
                            default:
                                // Unknown precondition (e.g. the removed "CAS") — terminal, not retried.
                                throw CreateTerminalException(context, status, detail);
                        }
                    }
                }
                    break;
                case StellarRetryStrings.TypeUrlBadRequest:
                    break;
            }
        }

        switch (status)
        {
            case StatusCode.NotFound:
                throw new DocumentNotFoundException(context);
            case StatusCode.Aborted:
                if (detail.Contains(StellarRetryStrings.CasMismatch)) throw new CasMismatchException(context);
                break;
            case StatusCode.FailedPrecondition:
                if (detail.Contains(StellarRetryStrings.Locked))
                {
                    // Same locked-document condition as the PreconditionFailure/LOCKED path above.
                    context.RetryReasons.Add(RetryReason.KvLocked);
                    request.Attempts++;
                    break;
                }
                if (detail.Contains(StellarRetryStrings.DocTooDeep)) throw new DocumentTooDeepException(context);
                if (detail.Contains(StellarRetryStrings.DocNotJson)) throw new DocumentNotJsonException(context);
                if (detail.Contains(StellarRetryStrings.PathMismatch)) throw new PathMismatchException(context);
                if (detail.Contains(StellarRetryStrings.ValueOutOfRange)) throw new ValueInvalidException();
                if (detail.Contains(StellarRetryStrings.PathValueOutOfRange)) throw new NumberTooBigException();
                if (detail.Contains(StellarRetryStrings.ValueTooLarge)) throw new ValueToolargeException(detail);
                // Unmapped FAILED_PRECONDITION is non-retryable (LOCKED aside) — terminal.
                throw CreateTerminalException(context, status, detail);
            case StatusCode.PermissionDenied:
            case StatusCode.Unauthenticated:
                throw new AuthenticationFailureException(context);
            case StatusCode.Cancelled:
                throw new RequestCanceledException(detail) { Context = context };
            case StatusCode.DeadlineExceeded:
                if (IsReadOnly(request)) throw new UnambiguousTimeoutException(detail, context);
                throw new AmbiguousTimeoutException(detail, context);
            case StatusCode.Internal:
                if (IsTransientGrpcTransportError(protoException))
                {
                    // HTTP/2 connection failures surface as StatusCode.Internal in .NET's
                    // gRPC library. These are transient transport issues, not server errors.
                    // Retry them like Unavailable, matching Java SDK behavior.
                    context.RetryReasons.Add(RetryReason.ServiceNotAvailable);
                    request.Attempts++;
                    break;
                }
                throw new InternalServerFailureException(detail);
            case StatusCode.Unavailable:
                context.RetryReasons.Add(RetryReason.ServiceNotAvailable);
                request.Attempts++;
                break;
            case StatusCode.InvalidArgument:
                throw new InvalidArgumentException(context);
            // RESOURCE_EXHAUSTED (rate-limit only, not in RFC 77) is unmapped — falls through to default.
            default:
                throw CreateTerminalException(context, status, detail);
        }
    }

    // Non-retryable terminal outcome: record the gRPC status and return a CouchbaseException for the
    // caller to throw.
    private static CouchbaseException CreateTerminalException(GenericErrorContext context, StatusCode status, string detail)
    {
        context.Fields.Add("status", status);
        return new CouchbaseException(context, detail);
    }

    internal virtual Any? StatusDeserializer(RpcException ex)
    {
        byte[]? statusBytes = null;
        foreach (Metadata.Entry me in ex.Trailers)
        {
            if (me.Key == "grpc-status-details-bin")
            {
                statusBytes = me.ValueBytes;
            }
        }

        if (statusBytes is null)
        {
            return null;
        }

        try
        {
            return Google.Rpc.Status.Parser.ParseFrom(statusBytes).Details[0];
        }
        catch (Exception)
        {
            return null;
        }
    }

    public Task<ResponseStatus> RetryAsync(BucketBase bucket, IOperation operation, CancellationTokenPair tokenPair = default)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Determines whether a gRPC Internal status error is actually a transient transport
    /// failure (e.g. HTTP/2 connection reset) rather than a genuine server-side error.
    /// In production, Grpc.Net.Client wraps transport exceptions (HttpRequestException,
    /// IOException) as the InnerException of the RpcException.
    /// </summary>
    private static bool IsTransientGrpcTransportError(RpcException ex)
    {
        return ex.InnerException is not null && IsTransientTransportException(ex.InnerException);
    }

    /// <summary>
    /// Determines whether an exception (or any exception in its InnerException chain)
    /// represents a transient transport failure that should be retried.
    /// </summary>
    internal static bool IsTransientTransportException(Exception? e)
    {
        while (e is not null)
        {
            if (e is HttpRequestException or System.IO.IOException)
            {
                return true;
            }
            e = e.InnerException;
        }
        return false;
    }
}
