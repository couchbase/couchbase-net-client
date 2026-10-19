#nullable enable
using System;
using System.Threading;

namespace Couchbase.Utils;

internal static class CancellationTokenExtensions
{
    /// <summary>
    /// Returns a new CancellationTokenSource with the given timeout value if the current token is CancellationToken.None.
    /// Otherwise, returns the current token.
    /// This is to help differentiate between users passing in existing CancellationTokens to operations, or specifying a
    /// timeout using a TimeSpan in Management operations' options.
    /// </summary>
    /// <param name="token">The current CancellationToken object.</param>
    /// <param name="timeout">The fallback Timeout value.</param>
    /// <returns>Either null or a new CancellationTokenSource which will cancel after the given Timeout.</returns>
    public static CancellationTokenSource? FallbackToTimeout(this CancellationToken token, TimeSpan timeout)
    {
        return token != CancellationToken.None
            ? null
            : new CancellationTokenSource(timeout);
    }

    /// <summary>
    /// Returns this CancellationTokenSource's token if it is non-null, or the passed in CancellationToken.
    /// </summary>
    /// <param name="cts">This CancellationTokenSource object.</param>
    /// <param name="token">The fallback CancellationToken.</param>
    /// <returns>Either the CTS's token or the fallback token.></returns>
    public static CancellationToken FallbackToToken(this CancellationTokenSource? cts, CancellationToken token)
    {
        return cts?.Token ?? token;
    }

    /// <summary>
    /// Cancels this CancellationTokenSource, then disposes it.
    /// </summary>
    /// <remarks>
    /// Use this when work that holds the token can still be running, such as the reads a stream
    /// leaves behind when its caller stops early. On .NET Framework, a registration on the token of
    /// a disposed source throws, but a registration on a cancelled token does not.
    /// </remarks>
    /// <param name="cts">This CancellationTokenSource object.</param>
    public static void CancelAndDispose(this CancellationTokenSource cts)
    {
        cts.Cancel();
        cts.Dispose();
    }
}
