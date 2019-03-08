using System.Threading.Tasks;

namespace Couchbase.Core.IO.Authentication
{ 
    /// <summary>
    /// Provides and interface for implementating a SASL authentication mechanism (CRAM MD5 or PLAIN).
    /// </summary>
    public interface ISaslMechanism
    {
        /// <summary>
        /// The username or Bucket name.
        /// </summary>
        string Username { get; }

        /// <summary>
        /// The password to authenticate against.
        /// </summary>
        string Password { get; }

        /// <summary>
        /// The type of SASL mechanism to use: PLAIN or CRAM MD5.
        /// </summary>
        string MechanismType { get; }

        /// <summary>
        /// Authenticates a username and password.
        /// </summary>
        /// <param name="connection">An implementation of <see cref="IConnection"/> which represents a TCP connection to a Couchbase Server.</param>
        /// <returns>True if succesful.</returns>
        Task<bool> AuthenticateAsync(IConnection connection);
    }
}
