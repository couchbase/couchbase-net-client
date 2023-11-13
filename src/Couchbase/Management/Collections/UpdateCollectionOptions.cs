using System;
using System.Threading;

namespace Couchbase.Management.Collections;

public class UpdateCollectionOptions
{
    internal CancellationToken TokenValue { get; private set; } = new CancellationTokenSource(ClusterOptions.Default.ManagementTimeout).Token;

    /// <summary>
    /// Allows to pass in a custom CancellationToken from a CancellationTokenSource.
    /// Note that issuing a CancellationToken will replace the Timeout if previously set.
    /// </summary>
    /// <param name="token">The Token to cancel the operation.</param>
    /// <returns></returns>
    public UpdateCollectionOptions CancellationToken(CancellationToken token)
    {
        TokenValue = token;
        return this;
    }

    /// <summary>
    /// Allows to set a Timeout for the operation.
    /// Note that issuing a Timeout will replace the CancellationToken if previously set.
    /// </summary>
    /// <param name="timeout">The duration of the Timeout. see <see cref="ClusterOptions"/> for the default value.</param>
    /// <returns></returns>
    public UpdateCollectionOptions Timeout(TimeSpan timeout)
    {
        TokenValue = new CancellationTokenSource(timeout).Token;
        return this;
    }

    public static UpdateCollectionOptions Default => new UpdateCollectionOptions();

    public static ReadOnly DefaultReadOnly => UpdateCollectionOptions.Default.AsReadOnly();

    public void Deconstruct(out CancellationToken tokenValue)
    {
        tokenValue = TokenValue;
    }

    public ReadOnly AsReadOnly()
    {
        this.Deconstruct(out CancellationToken tokenValue);
        return new ReadOnly(tokenValue);
    }

    public record ReadOnly(CancellationToken CancellationToken);
}
