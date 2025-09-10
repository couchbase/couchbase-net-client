using System.Security.Cryptography.X509Certificates;

namespace Couchbase.Core.IO.Authentication.X509;

public interface IRotatingCertificateFactory : ICertificateFactory
{
    bool HasUpdates { get; }

    void RefreshClientHandler(object state);
}
