#if NET6_0_OR_GREATER

using System;
using System.Security.Cryptography.X509Certificates;

#nullable enable

namespace Couchbase.UnitTests.Core.IO.Authentication.X509.Helpers;

/// <summary>
/// A trust bundle of independent certificate copies that disposes them with the test.
/// </summary>
/// <remarks>
/// Holding copies keeps the shared per-test certificates unaffected by whatever the platform does to the
/// bundle, which is the behaviour NCBC-4120 was about.
/// </remarks>
internal sealed class TrustBundle : IDisposable
{
    public TrustBundle(params X509Certificate2[] certs)
    {
        Certificates = new X509Certificate2Collection();
        foreach (var cert in certs)
        {
            Certificates.Add(TlsTestPki.CopyOf(cert));
        }
    }

    public X509Certificate2Collection Certificates { get; }

    public X509Certificate2 this[int index] => Certificates[index];

    public void Dispose()
    {
        foreach (var cert in Certificates)
        {
            cert.Dispose();
        }
    }
}

#endif
