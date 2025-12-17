using System;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Authentication.X509;

public class RotatingCertificateFactory : IRotatingCertificateFactory, IDisposable
{
    private readonly ICertificateFactory _certificateFactoryImplementation;
    private volatile X509Certificate2Collection _cachedCertificates = [];
    private readonly TimeSpan _interval;
    private readonly TimeSpan _expiresIn;
    private readonly ILogger? _logger;
    private volatile bool _disposed;
    private volatile bool _hasChanges;
    private readonly object _syncObj = new();
    private volatile Timer? _timer;

    public RotatingCertificateFactory(
        ICertificateFactory certificateFactoryImplementation, TimeSpan interval, TimeSpan expiresIn, ILogger? logger = null)
    {
        _certificateFactoryImplementation = certificateFactoryImplementation ?? throw new ArgumentNullException(nameof(certificateFactoryImplementation));
        _interval = interval;
        _expiresIn = expiresIn;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool HasUpdates => _hasChanges;

    public X509Certificate2Collection GetCertificates()
    {
        lock (_syncObj)
        {
            //if null it's a first request for certificates
            if (_cachedCertificates.Count == 0)
            {
                _ = Interlocked.Exchange(ref _cachedCertificates, _certificateFactoryImplementation.GetCertificates());

                _timer = TimerFactory.CreateWithFlowSuppressed(
                    RefreshCertificates!, this, _interval, _interval);
            }

            return _cachedCertificates;
        }
    }

    public void RefreshCertificates(object state)
    {
        lock (_syncObj)
        {
            var timer = _timer;
            if (timer == null)
            {
                return;
            }

            //pause timer
            timer.Change(Timeout.Infinite, Timeout.Infinite);
            try
            {
                //reset if its already been triggered to true earlier
                _hasChanges = false;

                var validNewCertificates = new X509Certificate2Collection();
                var possibleNewCertificates =
                    _certificateFactoryImplementation.GetCertificates();
                foreach (var certificate in possibleNewCertificates)
                {
                    var expirationDate = DateTime.Parse(certificate.GetExpirationDateString(), CultureInfo.InvariantCulture);
                    if (!_cachedCertificates.Contains(certificate) && expirationDate - DateTime.Today > _expiresIn)
                    {
                        validNewCertificates.Add(certificate);
                    }
                }

                if (validNewCertificates.Count > 0)
                {
                    _cachedCertificates =
                        Interlocked.Exchange(ref _cachedCertificates,
                            validNewCertificates);
                    _hasChanges = true;
                }
                else
                {
                    _logger?.LogDebug("No new certificates were found");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Refreshing client certificates failed");
            }
            finally
            {
                _timer?.Change(_interval, _interval);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            _disposed = true;
            _timer?.Dispose();
        }
    }
}
