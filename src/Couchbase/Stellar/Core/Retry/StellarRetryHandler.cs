using System;
using System.Net.Http;
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
using BucketNotFoundException = Couchbase.Core.Exceptions.BucketNotFoundException;
using CollectionNotFoundException = Couchbase.Core.Exceptions.CollectionNotFoundException;

#nullable enable

namespace Couchbase.Stellar.Core.Retry;

internal class StellarRetryHandler : IRetryOrchestrator
{
    private readonly TimeProvider _timeProvider;

    public StellarRetryHandler() : this(TimeProvider.System) { }

    /// <summary>
    /// Creates a StellarRetryHandler with a custom TimeProvider for deterministic testing.
    /// </summary>
    internal StellarRetryHandler(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public async Task<T> RetryAsync<T>(Func<Task<T>> send, IRequest request) where T : IServiceResult
    {
        var backoff = ControlledBackoff.Create(_timeProvider);
        var context = new GenericErrorContext();
        System.Type? finalErrorType = null;

        try
        {
            while (true)
            {
                if (request.Token.IsCancellationRequested)
                {
                    if (request.Idempotent)
                    {
                        throw new UnambiguousTimeoutException("The request timed out.", context);
                    }

                    throw new AmbiguousTimeoutException("The request timed out.", context);
                }

                try
                {
                    return await send().ConfigureAwait(false);
                }
                catch (RpcException e)
                {
                    if (e.StatusCode != StatusCode.OK)
                    {
                        HandleException(e, request, context);
                        await backoff.Delay(request).ConfigureAwait(false);
                    }
                }
                catch (Exception e) when (IsTransientTransportException(e))
                {
                    // HTTP/2 transport failures (e.g. connection reset, broken pipe) can
                    // surface as HttpRequestException or IOException rather than RpcException.
                    // Treat these like Unavailable and retry.
                    context.RetryReasons.Add(RetryReason.ServiceNotAvailable);
                    request.Attempts++;
                    await backoff.Delay(request).ConfigureAwait(false);
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
    /// Maps an <see cref="RpcException"/> raised while reading a streaming result <b>after</b> the
    /// first response has already been returned to the caller. Per RFC 77 (Streaming → Error
    /// handling), a mid-stream error cannot be retried because rows may already have been delivered:
    /// a retryable error is surfaced as <see cref="RequestCanceledException"/>, while a terminal
    /// error is mapped through the standard <see cref="HandleException"/> handling. This method
    /// always throws.
    /// </summary>
    internal void ThrowMidStreamException(Exception e, IRequest request)
    {
        var context = new GenericErrorContext();

        if (e is RpcException rpc && rpc.StatusCode != StatusCode.OK)
        {
            // Throws a mapped exception for terminal codes; for retryable codes it records a retry
            // reason on the context and returns (we must not actually retry mid-stream).
            HandleException(rpc, request, context);
        }
        else if (IsTransientTransportException(e))
        {
            // A transport failure (connection reset / broken pipe) that surfaced outside an
            // RpcException, mirroring RetryAsync's transient-transport catch — retryable.
            context.RetryReasons.Add(RetryReason.ServiceNotAvailable);
        }
        else
        {
            // Not a gRPC status error and not a recognised transient transport failure — nothing we
            // map here; let it propagate unchanged.
            throw e;
        }

        // Reached only for a retryable error. Per RFC 77 a mid-stream error cannot be retried (rows
        // may already have been delivered), so fail as RequestCancelled, carrying the SDK error
        // context (retry reason / server detail). Per RFC 77 we deliberately do NOT nest the raw gRPC
        // exception as an InnerException (TODO CNG-4: add lastException/lastError to the context).
        throw new RequestCanceledException(
            "A retryable error occurred mid-stream; the request was cancelled and cannot be retried.")
        {
            Context = context
        };
    }

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
                            case StellarRetryStrings.PreconditionCas:
                                throw new CasMismatchException(context);
                            case StellarRetryStrings.PreconditionLocked:
                            {
                                context.RetryReasons.Add(RetryReason.KvLocked);
                                throw new UnambiguousTimeoutException("Document is locked", context);
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
                    request.Attempts++;
                    break;
                }
                if (detail.Contains(StellarRetryStrings.DocTooDeep)) throw new DocumentTooDeepException(context);
                if (detail.Contains(StellarRetryStrings.DocNotJson)) throw new DocumentNotJsonException(context);
                if (detail.Contains(StellarRetryStrings.PathMismatch)) throw new PathMismatchException(context);
                if (detail.Contains(StellarRetryStrings.ValueOutOfRange)) throw new ValueInvalidException();
                if (detail.Contains(StellarRetryStrings.PathValueOutOfRange)) throw new NumberTooBigException();
                if (detail.Contains(StellarRetryStrings.ValueTooLarge)) throw new ValueToolargeException(detail);
                break;
            case StatusCode.PermissionDenied:
            case StatusCode.Unauthenticated:
                throw new AuthenticationFailureException(context);
            case StatusCode.Cancelled:
                throw new RequestCanceledException();
            case StatusCode.DeadlineExceeded:
                if (request.Idempotent) throw new UnambiguousTimeoutException(detail);
                throw new AmbiguousTimeoutException();
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
            case StatusCode.ResourceExhausted:
                throw new ValueToolargeException(
                    "Request exceeds maximum message size.");
            default:
            {
                context.Fields.Add("status", status);
                throw new CouchbaseException(context, protoException.Status.Detail);
            }
        }
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
