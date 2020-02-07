using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;

#nullable enable

namespace Couchbase.Utils
{
    /// <summary>
    /// Default implementation of <see cref="IIpEndPointService"/>.
    /// </summary>
    internal class IpEndPointService : IIpEndPointService
    {
        private readonly IDnsResolver _dnsResolver;
        private readonly ClusterOptions _clusterOptions;

        public IpEndPointService(IDnsResolver dnsResolver, ClusterOptions clusterOptions)
        {
            _dnsResolver = dnsResolver ?? throw new ArgumentNullException(nameof(dnsResolver));
            _clusterOptions = clusterOptions ?? throw new ArgumentNullException(nameof(clusterOptions));
        }

        /// <inheritdoc />
        public ValueTask<IPEndPoint?> GetIpEndPointAsync(NodesExt nodesExt, CancellationToken cancellationToken = default)
        {
            if (nodesExt == null)
            {
                throw new ArgumentNullException(nameof(nodesExt));
            }

            var port = _clusterOptions.EnableTls ? nodesExt.Services.KvSsl : nodesExt.Services.Kv;

            return GetIpEndPointAsync(nodesExt.Hostname, port, cancellationToken);
        }

        public async ValueTask<IPEndPoint?> GetIpEndPointAsync(string hostNameOrIpAddress, int port, CancellationToken cancellationToken = default)
        {
            if (hostNameOrIpAddress == null)
            {
                throw new ArgumentNullException(nameof(hostNameOrIpAddress));
            }

            if (!IPAddress.TryParse(hostNameOrIpAddress, out IPAddress? ipAddress))
            {
                ipAddress = await _dnsResolver.GetIpAddressAsync(hostNameOrIpAddress, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (ipAddress == null)
            {
                return null;
            }

            return new IPEndPoint(ipAddress, port);
        }
    }
}
