using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Core.IO.Authentication.X509;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.IO.Connections;

#nullable enable

internal class CallbackCreator
{
    private readonly bool _ignoreNameMismatch;
    ILogger<object> _sslLogger;
    private readonly IRedactor _redactor;
    private X509Certificate2Collection? _certs;

    public CallbackCreator(
        bool ignoreNameMismatch,
        ILogger<object> sslLogger,
        IRedactor redactor,
        X509Certificate2Collection? certs
        )
    {
        _ignoreNameMismatch = ignoreNameMismatch;
        _sslLogger = sslLogger ?? throw new ArgumentNullException(nameof(sslLogger));
        _redactor = redactor;
        _certs =  certs;
    }

    /// <summary>
    /// Validates the server certificate. Returns false rather than throwing for any rejection, so a
    /// failure is reported as a certificate error instead of an unrelated exception.
    /// </summary>
    public bool Callback(object sender, X509Certificate? certificate, X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        // A server that presents no certificate (RemoteCertificateNotAvailable) is a rejection, not a bug.
        if (certificate is null || chain is null)
        {
            _sslLogger.LogInformation("X509 no server certificate was presented ({sslPolicyErrors}).", sslPolicyErrors);
            return false;
        }

        if (sslPolicyErrors == SslPolicyErrors.None)
        {
            _sslLogger.LogDebug("X509 Validation passed");
            return true;
        }

        if (!_ignoreNameMismatch)
        {
            if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) !=
                SslPolicyErrors.None)
            {
                _sslLogger.LogInformation(
                    "X509 Certificate name mismatch error."); // and possibly other issues
                return false;
            }
        }
        else
        {
            if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) !=
                SslPolicyErrors.None)
            {
                _sslLogger.LogDebug("X509 Ignoring Certificate name mismatch error.");
            }
        }

        if (_certs != null)
        {
            _sslLogger.LogDebug("X509 using user-provided certificate(s) for validation (count: {certCount})", _certs.Count);
            var customCertsCallback =
                CertificateFactory.GetValidatorWithPredefinedCertificates(_certs, _sslLogger,
                    _redactor);
            return customCertsCallback(sender, certificate, chain, sslPolicyErrors);
        }

        _sslLogger.LogDebug("X509 using default certificate(s) for validation (Capella CA)");
        var defaultCallback =
            CertificateFactory.GetValidatorWithDefaultCertificates(_sslLogger, _redactor);
        return defaultCallback(sender, certificate, chain, sslPolicyErrors);
    }
}
