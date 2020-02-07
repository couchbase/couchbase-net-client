using System;
using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
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

        // Cache the results on a per node adapter basis, this way if a new node adapter is created we'll repeat
        // the DNS lookup. This will help address topology changes hidden behind DNS over the lifetime of the application.
        // Using a ConditionalWeakTable ensures that we don't hold references to node adapters so they can be garbage collected.
        private readonly ConditionalWeakTable<NodeAdapter, ConcurrentDictionary<string, ValueTask<IPEndPoint?>>> _nodeAdapterCache =
            new ConditionalWeakTable<NodeAdapter, ConcurrentDictionary<string, ValueTask<IPEndPoint?>>>();

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

        /// <inheritdoc />
        public ValueTask<IPEndPoint?> GetIpEndPointAsync(NodeAdapter nodeAdapter, CancellationToken cancellationToken = default)
        {
            if (nodeAdapter == null)
            {
                throw new ArgumentNullException(nameof(nodeAdapter));
            }

            var port = _clusterOptions.EnableTls ? nodeAdapter.KeyValueSsl : nodeAdapter.KeyValue;
            var key = $"{nodeAdapter.Hostname}:{port}";

            var cache = _nodeAdapterCache.GetOrCreateValue(nodeAdapter);
            return cache.GetOrAdd(key, _ => GetIpEndPointAsync(nodeAdapter.Hostname, port, cancellationToken));
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
