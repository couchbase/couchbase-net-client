#nullable enable
using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.IO.Authentication.X509;

public class DelegatingCertificateFactory(
    ICertificateFactory certificateFactory, ILogger? logger = null) : IRotatingCertificateFactory
{
    private ICertificateFactory _certificateFactory = certificateFactory ?? throw new ArgumentNullException(nameof(certificateFactory));
    private readonly ILogger? _logger = logger;
    private volatile bool _hasUpdates;
    private volatile X509Certificate2Collection _cachedCertificates = new();
    private readonly object _syncObj = new();

    public bool HasUpdates => _hasUpdates;

    public void RefreshCertificates(object state)
    {
        var expiresIn = (TimeSpan)state;
        lock (_syncObj)
        {
            //reset if its already been triggered to true earlier
            _hasUpdates = false;

            var usable = ClientCertificateSelection.SelectUsable(
                _certificateFactory.GetCertificates(), expiresIn);

            if (usable.Count == 0)
            {
                _logger?.LogWarning("No usable client certificates were found, keeping the current certificates");
            }
            else if (ClientCertificateSelection.HasSameCertificates(usable, _cachedCertificates))
            {
                _logger?.LogDebug("Client certificates are unchanged");
            }
            else
            {
                _cachedCertificates = usable;
                _hasUpdates = true;
            }
        }
    }

    public X509Certificate2Collection GetCertificates()
    {
        lock (_syncObj)
        {
            //if null it's a first request for certificates
            if (_cachedCertificates.Count == 0)
            {
                _cachedCertificates = _certificateFactory.GetCertificates();
                _hasUpdates = true;
            }

            return _cachedCertificates;
        }
    }
}
