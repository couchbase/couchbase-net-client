#if NET6_0_OR_GREATER

using System;
using Xunit;

#nullable enable

namespace Couchbase.UnitTests.Core.IO.Authentication.X509.Helpers;

/// <summary>
/// Assertions that hold the validator to account for the verdict, not just for the handshake outcome.
/// </summary>
internal static class HandshakeAssert
{
    public static void Accepted(HandshakeResult result, string because)
    {
        RanCleanly(result, because);
        Assert.True(result.ValidatorVerdict, because);
        Assert.True(result.Accepted, because);
    }

    public static void RejectedByValidator(HandshakeResult result, string because)
    {
        RanCleanly(result, because);
        Assert.False(result.ValidatorVerdict, because);
        Assert.False(result.Accepted, because);
    }

    private static void RanCleanly(HandshakeResult result, string because)
    {
        if (!result.ValidatorInvoked)
        {
            Assert.Fail($"{because}{Environment.NewLine}" +
                        "The validator was never invoked, so the handshake result says nothing about it.");
        }

        if (result.ValidatorException is not null)
        {
            Assert.Fail($"{because}{Environment.NewLine}" +
                        $"The validator threw instead of returning a verdict: {result.ValidatorException}");
        }
    }
}

#endif
