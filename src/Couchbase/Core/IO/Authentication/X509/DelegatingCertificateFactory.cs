#nullable enable
using System;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
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

            var validNewCertificates = new X509Certificate2Collection();
            var possibleNewCertificates =
                _certificateFactory.GetCertificates();
            foreach (var certificate in possibleNewCertificates)
            {
                var expirationDate = DateTime.Parse(certificate.GetExpirationDateString(), CultureInfo.InvariantCulture);
                if (!_cachedCertificates.Contains(certificate) && expirationDate - DateTime.Today > expiresIn)
                {
                    validNewCertificates.Add(certificate);
                }
            }

            if (validNewCertificates.Count > 0)
            {
                _cachedCertificates =
                    Interlocked.Exchange(ref _cachedCertificates,
                        validNewCertificates);
                _hasUpdates = true;
            }
            else
            {
                _logger?.LogDebug("No new certificates were found");
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
                _ = Interlocked.Exchange(ref _cachedCertificates, _certificateFactory.GetCertificates());
                _hasUpdates = true;
            }

            return _cachedCertificates;
        }
    }
}
