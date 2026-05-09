using System;
using System.Collections.Generic;
using System.Threading;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Retry;

#nullable enable
namespace Couchbase.Stellar.Core.Retry;

/*
 * Wraps a gRPC response and handles the retry actions inside StellarRetryHandler.
 * Is returned to the caller but not to the user.
 */
public class StellarRequest : IRequest
{
    private readonly TimeProvider _timeProvider;

    public StellarRequest() : this(TimeProvider.System) { }

    /// <summary>
    /// Creates a StellarRequest with a custom TimeProvider for deterministic testing.
    /// </summary>
    internal StellarRequest(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        CreatedAt = _timeProvider.GetUtcNow().UtcDateTime;
    }

    public uint Attempts { get; set; }
    public bool Idempotent { get; set; }
    public List<RetryReason> RetryReasons { get; set; } = new();
    public IRetryStrategy RetryStrategy { get; set; } = new BestEffortRetryStrategy();
    public TimeSpan Timeout { get; set; }
    public TimeSpan Elapsed { get; }

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

    public void StopRecording()
    {
        throw new NotImplementedException();
    }

    public void StopRecording(Type? errorType)
    {
        throw new NotImplementedException();
    }

    public IValueRecorder Recorder { get; set; } = null!;
    public void LogOrphaned()
    {
        throw new NotImplementedException();
    }

    public GenericErrorContext Context { get; set; } = new();
}
