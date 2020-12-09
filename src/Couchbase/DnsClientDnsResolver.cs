using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Utils;
using DnsClient;
using DnsClient.Protocol;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase
{
    /// <summary>
    /// Default implementation of <see cref="IDnsResolver"/>.
    /// </summary>
    internal class DnsClientDnsResolver : IDnsResolver
    {
        private const string DefaultServicePrefix = "_couchbase._tcp.";
        private const string TlsServicePrefix = "_couchbases._tcp.";
        private readonly ILookupClient _lookupClient;
        private readonly IDotNetDnsClient _dotNetDnsClient;
        private readonly ILogger<DnsClientDnsResolver> _logger;

        public IpAddressMode IpAddressMode { get; set; }

        /// <summary>
        /// Used to disable DNS SRV resolution, enabled by default.
        /// </summary>
        public bool EnableDnsSrvResolution { get; set; } = true;

        public DnsClientDnsResolver(ILookupClient lookupClient, IDotNetDnsClient dotNetDnsClient, ILogger<DnsClientDnsResolver> logger)
        {
            _lookupClient = lookupClient ?? throw new ArgumentNullException(nameof(lookupClient));
            _dotNetDnsClient = dotNetDnsClient ?? throw new ArgumentNullException(nameof(dotNetDnsClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IPAddress?> GetIpAddressAsync(string hostName,
            CancellationToken cancellationToken = default)
        {
            var addresses = (IEnumerable<IPAddress>) await _dotNetDnsClient.GetHostAddressesAsync(hostName).ConfigureAwait(false);

            if (IpAddressMode == IpAddressMode.ForceIpv6)
            {
                addresses = addresses.Where(p => p.AddressFamily == AddressFamily.InterNetworkV6);
            }
            else if (IpAddressMode == IpAddressMode.ForceIpv4)
            {
                addresses = addresses.Where(p => p.AddressFamily == AddressFamily.InterNetwork);
            }

            var preferredAddresses = IpAddressMode switch
            {
                // ReSharper disable PossibleMultipleEnumeration
                IpAddressMode.PreferIpv4 => addresses.Where(p => p.AddressFamily == AddressFamily.InterNetwork),
                IpAddressMode.PreferIpv6 => addresses.Where(p => p.AddressFamily == AddressFamily.InterNetworkV6),
                IpAddressMode.Default => addresses.Where(p => p.AddressFamily == AddressFamily.InterNetworkV6),
                // ReSharper restore PossibleMultipleEnumeration
                _ => null
            };

            var preferredIpAddress = preferredAddresses?.FirstOrDefault();
            if (preferredIpAddress != null)
            {
                return preferredIpAddress;
            }

            // ReSharper disable once PossibleMultipleEnumeration
            return addresses.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<IEnumerable<HostEndpoint>> GetDnsSrvEntriesAsync(Uri bootstrapUri,
            CancellationToken cancellationToken = default)
        {
            if (!EnableDnsSrvResolution)
            {
                return Enumerable.Empty<HostEndpoint>();
            }

            var query = string.Concat(bootstrapUri.Scheme == "couchbases" ? TlsServicePrefix : DefaultServicePrefix, bootstrapUri.Host);
            var result = await _lookupClient.QueryAsync(query, QueryType.SRV,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.HasError)
            {
                _logger.LogInformation("There was an error attempting to resolve hosts using DNS-SRV - {errorMessage}", result.ErrorMessage);
                return Enumerable.Empty<HostEndpoint>();
            }

            var records = result.Answers.SrvRecords().ToList();
            if (!records.Any())
            {
                _logger.LogInformation("No DNS SRV records found.");
                return Enumerable.Empty<HostEndpoint>();
            }

            return records
                .OrderBy(record => record.Priority)
                .Select(record =>
                {
                    var host = record.Target.Value;
                    if (host.EndsWith("."))
                    {
                        var index = host.LastIndexOf(".", StringComparison.Ordinal);
                        host = host.Substring(0, index);
                    }

                    return new HostEndpoint(host, record.Port);
                });
        }
    }
}
