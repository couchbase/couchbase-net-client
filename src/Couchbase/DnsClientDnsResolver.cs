using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Logging;
using DnsClient;
using Microsoft.Extensions.Logging;

namespace Couchbase
{
    internal class DnsClientDnsResolver : IDnsResolver
    {
        private const string DefaultServicePrefix = "_couchbase._tcp.";
        private static readonly ILogger Logger = LogManager.CreateLogger<DnsClientDnsResolver>();
        private static readonly List<Uri> EmptyList = new List<Uri>();
        private readonly ILookupClient LookupClient = new LookupClient();

        internal DnsClientDnsResolver()
            : this (new LookupClient())
        { }

        internal DnsClientDnsResolver(ILookupClient lookupClient)
        {
            LookupClient = lookupClient;
        }

        public async Task<IEnumerable<Uri>> GetDnsSrvEntriesAsync(Uri bootstrapUri)
        {
            var query = string.Concat(DefaultServicePrefix, bootstrapUri.Host);
            var result = await LookupClient.QueryAsync(query, QueryType.SRV);

            if (result.HasError)
            {
                Logger.LogInformation($"There was an error attempting to resolve hosts using DNS-SRV - {result.ErrorMessage}");
                return EmptyList;
            }

            var records = result.Answers.SrvRecords();
            if (!records.Any())
            {
                Logger.LogInformation($"No DNS SRV records found.");
                return EmptyList;
            }

            return records
                .OrderBy(record => record.Priority)
                .Select(record =>
                {
                    var host = record.Target.Value;
                    if (host.EndsWith("."))
                    {
                        var index = host.LastIndexOf(".");
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
