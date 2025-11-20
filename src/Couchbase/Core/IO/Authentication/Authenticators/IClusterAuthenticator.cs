namespace Couchbase.Core.IO.Authentication.Authenticators;

public interface IClusterAuthenticator
{
    /// <summary>
    /// Changes the instance of the <see cref="IAuthenticator"/> used by the cluster.
    /// Existing connections will not be affected, but all new connections will use the new authenticator.
    /// </summary>
    /// <param name="authenticator">The new <see cref="IAuthenticator"/> to use.</param>
    void Authenticator(IAuthenticator authenticator);
}
