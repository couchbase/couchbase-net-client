using System;
using System.Threading;
using Couchbase.Utils;
using CancellationTokenCls = System.Threading.CancellationToken;

namespace Couchbase.Management.Collections;

public class UpdateCollectionOptions
{
    internal CancellationToken TokenValue { get; private set; } = CancellationTokenCls.None;
    internal TimeSpan TimeoutValue { get; set; } = ClusterOptions.Default.ManagementTimeout;

    /// <summary>
    /// Allows to pass in a custom CancellationToken from a CancellationTokenSource.
    /// Note that CancellationToken() takes precedence over Timeout(). If both CancellationToken and Timeout are set, the former will be used in the operation.
    /// </summary>
    /// <param name="token">The Token to cancel the operation.</param>
    /// <returns>This class for method chaining.</returns>
    public UpdateCollectionOptions CancellationToken(CancellationToken token)
    {
        TokenValue = token;
        return this;
    }

    /// <summary>
    /// Allows to set a Timeout for the operation.
    /// Note that CancellationToken() takes precedence over Timeout(). If both CancellationToken and Timeout are set, the former will be used in the operation.
    /// </summary>
    /// <param name="timeout">The duration of the Timeout. Set to 75s by default.</param>
    /// <returns>This class for method chaining.</returns>
    public UpdateCollectionOptions Timeout(TimeSpan timeout)
    {
        TimeoutValue = timeout;
        return this;
    }

    public static UpdateCollectionOptions Default => new UpdateCollectionOptions();

    public static ReadOnly DefaultReadOnly => Default.AsReadOnly();

    public void Deconstruct(out CancellationToken tokenValue, out TimeSpan timeoutValue)
    {
        tokenValue = TokenValue;
        timeoutValue = TimeoutValue;
    }

    public ReadOnly AsReadOnly()
    {
        this.Deconstruct(out CancellationToken tokenValue, out TimeSpan timeoutValue);
        return new ReadOnly(tokenValue, timeoutValue);
    }

    public record ReadOnly(CancellationToken CancellationToken, TimeSpan Timeout);
}
