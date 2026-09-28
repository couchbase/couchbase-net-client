#nullable enable
using System;
using System.Security.Cryptography.X509Certificates;

namespace Couchbase.Core.IO.Authentication.X509;

internal static class ClientCertificateSelection
{
    internal static X509Certificate2Collection SelectUsable(X509Certificate2Collection candidates, TimeSpan expiresIn)
    {
        var usable = new X509Certificate2Collection();
        foreach (X509Certificate2 certificate in candidates)
        {
            if (certificate.NotAfter - DateTime.Today > expiresIn && !Contains(usable, certificate))
            {
                usable.Add(certificate);
            }
        }

        return usable;
    }

    internal static bool HasSameCertificates(X509Certificate2Collection a, X509Certificate2Collection b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (X509Certificate2 certificate in a)
        {
            if (!Contains(b, certificate))
            {
                return false;
            }
        }

        return true;
    }

    // X509Certificate.Equals compares only issuer and serial number, so match on the thumbprint instead.
    private static bool Contains(X509Certificate2Collection collection, X509Certificate2 certificate)
    {
        foreach (X509Certificate2 candidate in collection)
        {
            if (string.Equals(candidate.Thumbprint, certificate.Thumbprint, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
