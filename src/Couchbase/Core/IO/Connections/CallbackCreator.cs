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

    public bool Callback(object sender, X509Certificate? certificate, X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {

        certificate = certificate ?? throw new ArgumentNullException(nameof(certificate));
        chain = chain ?? throw new ArgumentNullException(nameof(chain));

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
