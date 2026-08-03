#if NET6_0_OR_GREATER

using System.Collections.Generic;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

#nullable enable

namespace Couchbase.UnitTests.Core.IO.Authentication.X509.Helpers;

/// <summary>
/// Invokes a validation callback against a chain populated by hand.
/// </summary>
/// <remarks>
/// For the inputs a real handshake cannot deliver. SslStreamCertificateContext drops self-signed
/// certificates from the chain it serves, so a server-presented root only reaches the validator this way.
/// </remarks>
internal static class DirectChain
{
    public static bool Validate(
        RemoteCertificateValidationCallback validator,
        X509Certificate2 serverLeaf,
        IEnumerable<X509Certificate2> wireCerts,
        SslPolicyErrors errors = SslPolicyErrors.RemoteCertificateChainErrors)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

        // Copies, so the validator's own cleanup cannot reach the caller's certificates.
        var copies = new List<X509Certificate2>();
        try
        {
            foreach (var wireCert in wireCerts)
            {
                var copy = TlsTestPki.CopyOf(wireCert);
                copies.Add(copy);
                chain.ChainPolicy.ExtraStore.Add(copy);
            }

            return validator(new object(), serverLeaf, chain, errors);
        }
        finally
        {
            chain.ChainPolicy.ExtraStore.Clear();
            foreach (var copy in copies)
            {
                copy.Dispose();
            }
        }
    }
}

#endif
