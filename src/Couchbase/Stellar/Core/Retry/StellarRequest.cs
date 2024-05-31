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
    public uint Attempts { get; set; }
    public bool Idempotent { get; set; }
    public List<RetryReason> RetryReasons { get; set; } = new();
    public IRetryStrategy RetryStrategy { get; set; } = new BestEffortRetryStrategy();
    public TimeSpan Timeout { get; set; }
    public TimeSpan Elapsed { get; }
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
    public int MaxRetryAttempts { get; set; } = 10;
}
