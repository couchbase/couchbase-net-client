namespace Couchbase.Authentication
{
    /// <summary>
    /// Interface for all Authenticators.
    /// </summary>
    public interface IAuthenticator
    {
        /// <summary>
        /// Gets the type of the authenticator.
        /// </summary>
        AuthenticatorType AuthenticatorType { get; }
    }
}
