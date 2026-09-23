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
            if (certificate.NotAfter - DateTime.Today > expiresIn && !usable.Contains(certificate))
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
            if (!b.Contains(certificate))
            {
                return false;
            }
        }

        return true;
    }
}
