using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DnsClient;
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
        private readonly ILookupClient _lookupClient;
        private readonly ILogger<DnsClientDnsResolver> _logger;

        public DnsClientDnsResolver(ILookupClient lookupClient, ILogger<DnsClientDnsResolver> logger)
        {
            _lookupClient = lookupClient ?? throw new ArgumentNullException(nameof(lookupClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Uri>> GetDnsSrvEntriesAsync(Uri bootstrapUri,
            CancellationToken cancellationToken = default)
        {
            var query = string.Concat(DefaultServicePrefix, bootstrapUri.Host);
            var result = await _lookupClient.QueryAsync(query, QueryType.SRV,
                cancellationToken: cancellationToken);

            if (result.HasError)
            {
                _logger.LogInformation("There was an error attempting to resolve hosts using DNS-SRV - {errorMessage}", result.ErrorMessage);
                return Enumerable.Empty<Uri>();
            }

            var records = result.Answers.SrvRecords().ToList();
            if (!records.Any())
            {
                _logger.LogInformation("No DNS SRV records found.");
                return Enumerable.Empty<Uri>();
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
                    return new UriBuilder
                    {
                        Scheme = bootstrapUri.Scheme,
                        Host = host,
                        Port = record.Port
                    }.Uri;
                });
        }
    }
}
