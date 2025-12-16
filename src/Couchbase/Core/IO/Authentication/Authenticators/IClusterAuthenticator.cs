using Couchbase.Core.IO.Authentication.X509;

namespace Couchbase.Core.IO.Authentication.Authenticators;

public interface IClusterAuthenticator
{
    /// <summary>
    /// Changes the instance of the <see cref="IAuthenticator"/> used by the cluster.
    ///
    /// This method is meant to be used to update the JWT in use by the connections when it expires.
    ///
    /// For rotating mTLS client certificates, use an implementation of <see cref="IRotatingCertificateFactory"/> directly when
    /// configuring your <see cref="ClusterOptions"/>.
    /// </summary>
    /// <param name="authenticator">The new <see cref="IAuthenticator"/> to use.</param>
    /// <remarks>
    /// <para>
    /// <b>Note:</b> The type of <see cref="IAuthenticator"/> should not change at runtime.
    /// If you used Password authentication when creating the cluster, you should continue to use
    /// Password authentication when changing the authenticator.
    /// </para>
    /// For <see cref="JwtAuthenticator"/>: In addition to applying to new connections, the SDK will
    /// asynchronously re-authenticate all existing KV connections using the new JWT.
    /// </remarks>
    void Authenticator(IAuthenticator authenticator);
}
