using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Connections;

#nullable enable

namespace Couchbase.Core.IO.Authentication
{
    /// <summary>
    /// Provides and interface for implementing a SASL authentication mechanism (CRAM MD5 or PLAIN).
    /// </summary>
    internal interface ISaslMechanism
    {
        /// <summary>
        /// The type of SASL mechanism to use: PLAIN, CRAM MD5, etc.
        /// </summary>
        MechanismType MechanismType { get; }

        /// <summary>
        /// Authenticates a username and password.
        /// </summary>
        /// <param name="connection">An implementation of <see cref="IConnection"/> which represents a TCP connection to a Couchbase Server.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if successful.</returns>
        Task AuthenticateAsync(IConnection connection, CancellationToken cancellationToken = default);
    }
}
