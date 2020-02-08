using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.Sharding
{
    /// <summary>
    /// Default implementation of <see cref="IVBucketServerMapFactory"/>.
    /// </summary>
    internal class VBucketServerMapFactory : IVBucketServerMapFactory
    {
        private readonly IIpEndPointService _ipEndPointService;

        public VBucketServerMapFactory(IIpEndPointService ipEndPointService)
        {
            _ipEndPointService = ipEndPointService ?? throw new ArgumentNullException(nameof(ipEndPointService));
        }

        /// <inheritdoc />
        public async Task<VBucketServerMap> CreateAsync(VBucketServerMapDto serverMapDto,
            CancellationToken cancellationToken = default) =>
            new VBucketServerMap(serverMapDto,
                await GetIpEndPointsAsync(serverMapDto.ServerList, cancellationToken).ToListAsync(cancellationToken)
                    .ConfigureAwait(false));

        private async IAsyncEnumerable<IPEndPoint> GetIpEndPointsAsync(IEnumerable<string> serverList,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var server in serverList)
            {
                var (hostName, port) = ParseServer(server);

                var ipEndPoint = await _ipEndPointService.GetIpEndPointAsync(hostName, port, cancellationToken)
                    .ConfigureAwait(false);
                if (ipEndPoint == null)
                {
                    throw new ArgumentException($"Unable to resolve '{server}'.", nameof(serverList));
                }

                yield return ipEndPoint;
            }
        }

        /// <summary>
        /// Internal for unit testing, not intended to be consumed directly.
        /// </summary>
        internal static (string HostName, int Port) ParseServer(string server) =>
            server.StartsWith("[", StringComparison.Ordinal) ?
                ParseIpv6Server(server) :
                ParseBasicServer(server);

        private static (string HostName, int Port) ParseBasicServer(string server)
        {
            const int maxSplits = 2;
            var address = server.Split(':');
            if (address.Length != maxSplits)
            {
                throw new ArgumentException("server");
            }
            if (!int.TryParse(address[1], out var port))
            {
                throw new ArgumentException("port");
            }

            return (address[0], port);
        }

        private static (string HostName, int Port) ParseIpv6Server(string server)
        {
            // Assumes an address with IPv6 syntax of "[ip]:port"
            // Since ip will contain colons, we can't just split the string

            const string invalidServer = "Not a valid IPv6 host/port string";

            var addressEnd = server.IndexOf(']', 1);
            if (addressEnd < 0)
            {
                throw new ArgumentException(invalidServer, nameof(server));
            }

            var address = server.Substring(1, addressEnd - 1);

            if (server.Length < addressEnd + 3 || server[addressEnd + 1] != ':')
            {
                // Doesn't have the port on the end
                throw new ArgumentException(invalidServer, nameof(server));
            }

            var portString = server.Substring(addressEnd + 2);
            if (!int.TryParse(portString, out var port))
            {
                throw new ArgumentException(invalidServer, nameof(server));
            }

            return (address, port);
        }
    }
}
