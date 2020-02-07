using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;

#nullable enable

namespace Couchbase.Utils
{
    /// <summary>
    /// Provides helpers for resolving IP endpoints.
    /// </summary>
    internal interface IIpEndPointService
    {
        /// <summary>
        /// Returns the K/V <see cref="IPEndPoint"/> for a given <see cref="NodesExt"/>.
        /// </summary>
        /// <param name="nodesExt">The <see cref="NodesExt"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The IP endpoint, or null if the name could not be resolved.</returns>
        ValueTask<IPEndPoint?> GetIpEndPointAsync(NodesExt nodesExt, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns the <see cref="IPEndPoint"/> for a given hostname and port. Hostname may be an IP addres.
        /// </summary>
        /// <param name="hostNameOrIpAddress">The host name or IP address.</param>
        /// <param name="port">The port number.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/>.</param>
        /// <returns>The IP endpoint, or null if the name could not be resolved.</returns>
        /// <remarks>If providing an IPv6 address, wrapping in square brackets (i.e. [::1]) is acceptable.</remarks>
        ValueTask<IPEndPoint?> GetIpEndPointAsync(string hostNameOrIpAddress, int port,
            CancellationToken cancellationToken = default);
    }
}
