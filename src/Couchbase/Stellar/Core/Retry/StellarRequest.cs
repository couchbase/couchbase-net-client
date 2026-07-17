using System;
using System.Collections.Generic;
using System.Threading;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Retry;
using Couchbase.Utils;

#nullable enable
namespace Couchbase.Stellar.Core.Retry;

/*
 * Wraps a gRPC response and handles the retry actions inside StellarRetryHandler.
 * Is returned to the caller but not to the user.
 */
public class StellarRequest : IRequest
{
    private readonly TimeProvider _timeProvider;
    private LightweightStopwatch _stopwatch;

    public StellarRequest() : this(TimeProvider.System) { }

    /// <summary>
    /// Creates a StellarRequest with a custom TimeProvider for deterministic testing.
    /// </summary>
    internal StellarRequest(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        CreatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        _stopwatch = LightweightStopwatch.StartNew();
    }

    public uint Attempts { get; set; }
    public bool Idempotent { get; set; }

    private bool? _readOnly;

    /// <summary>
    /// Whether the operation only reads server state (i.e. never mutates data, expiry, or locks).
    /// This drives ambiguous-vs-unambiguous timeout classification: a read-only op that times out
    /// definitively did not mutate state (<see cref="Couchbase.Core.Exceptions.UnambiguousTimeoutException"/>),
    /// whereas a mutating op might have (<see cref="Couchbase.Core.Exceptions.AmbiguousTimeoutException"/>).
    /// Mirrors the classic SDK, where <c>IRequest.Idempotent</c> is defined as <c>IsReadOnly</c>
    /// (see OperationBase). Defaults to <see cref="Idempotent"/> when not set explicitly, as the two
    /// coincide for most operations; KV operations that are idempotent yet mutate state
    /// (GetAndLock, GetAndTouch, MutateIn) set this to <c>false</c> explicitly.
    /// </summary>
    public bool ReadOnly
    {
        get => _readOnly ?? Idempotent;
        set => _readOnly = value;
    }

    public List<RetryReason> RetryReasons { get; set; } = new();
    public IRetryStrategy RetryStrategy { get; set; } = new BestEffortRetryStrategy();
    public TimeSpan Timeout { get; set; }
    public TimeSpan Elapsed => _stopwatch.Elapsed;

    /// <summary>
    /// The time this request was created. Used to compute remaining timeout.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Returns the remaining time before this request should time out,
    /// or null if no timeout was set (Timeout is zero).
    /// Used to set shrinking gRPC deadlines on each retry attempt.
    /// </summary>
    public TimeSpan? RemainingTimeout =>
        Timeout > TimeSpan.Zero
            ? Timeout - (_timeProvider.GetUtcNow().UtcDateTime - CreatedAt)
            : null;
    public CancellationToken Token { get; set; }
    public string? ClientContextId { get; set; }
    public string? Statement { get; set; }

    internal string? ServiceName { get; set; }
    internal string? OperationName { get; set; }
    internal string? BucketName { get; set; }
    internal string? ScopeName { get; set; }
    internal string? CollectionName { get; set; }
    internal IRequestSpan? Span { get; set; }
    private bool _recordingStopped;

    /// <summary>
    /// Configures the telemetry context for this request.
    /// Must be called before the request enters the retry loop.
    /// </summary>
    internal void SetMetrics(
        string serviceName,
        string operationName,
        IRequestSpan? span,
        string? bucketName = null,
        string? scopeName = null,
        string? collectionName = null)
    {
        ServiceName = serviceName;
        OperationName = operationName;
        Span = span;
        BucketName = bucketName;
        ScopeName = scopeName;
        CollectionName = collectionName;
    }

    public void StopRecording()
    {
        StopRecording(errorType: null);
    }

    public void StopRecording(System.Type? errorType)
    {
        if (_recordingStopped) return;
        _recordingStopped = true;

        var elapsed = _stopwatch.Elapsed;

        Span?.SetStatus(errorType == null
            ? RequestSpanStatusCode.Ok
            : RequestSpanStatusCode.Error);
        Span?.SetAttribute(OuterRequestSpans.Attributes.Retries, Attempts);

        if (ServiceName is not null && OperationName is not null)
        {
            MetricTracker.Stellar.TrackOperation(
                ServiceName,
                OperationName,
                elapsed,
                errorType,
                BucketName,
                ScopeName,
                CollectionName,
                Span);
        }
    }

    public IValueRecorder Recorder { get; set; } = NoopValueRecorder.Instance;
    public void LogOrphaned()
    {
        // No orphan tracking for Stellar/gRPC requests
    }

    public GenericErrorContext Context { get; set; } = new();
}
