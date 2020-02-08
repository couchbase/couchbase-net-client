using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Analytics;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DataMapping;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Retry;
using Couchbase.Core.Sharding;
using Couchbase.Management.Buckets;
using Couchbase.Management.Query;
using Couchbase.Management.Search;
using Couchbase.Management.Users;
using Couchbase.Query;
using Couchbase.Search;
using Couchbase.Utils;
using Couchbase.Views;
using DnsClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Couchbase.Core.DI
{
    internal static class DefaultServices
    {
        /// <summary>
        /// Provides the default services for a new service provider.
        /// </summary>
        /// <returns>The default services. This collection can be safely modified without side effects.</returns>
        public static IDictionary<Type, IServiceFactory> GetDefaultServices() =>
            GetDefaultServicesEnumerable().ToDictionary(p => p.Type, p => p.Factory);

        private static IEnumerable<(Type Type, IServiceFactory Factory)> GetDefaultServicesEnumerable()
        {
            yield return (typeof(ILoggerFactory), new SingletonServiceFactory(new NullLoggerFactory()));
            yield return (typeof(ILogger<>), new SingletonGenericServiceFactory(typeof(Logger<>)));

            yield return (typeof(ILookupClient), new TransientServiceFactory(_ => new LookupClient()));
            yield return (typeof(IDnsResolver), new SingletonServiceFactory(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<ClusterOptions>();

                return new DnsClientDnsResolver(
                    serviceProvider.GetRequiredService<ILookupClient>(),
                    serviceProvider.GetRequiredService<ILogger<DnsClientDnsResolver>>())
                {
                    IpAddressMode = options.ForceIPv4 ? IpAddressMode.ForceIpv4 : IpAddressMode.Default,
                    EnableDnsSrvResolution = options.EnableDnsSrvResolution
                };
            }));
            yield return (typeof(IIpEndPointService), new TransientServiceFactory(typeof(IpEndPointService)));

            yield return (typeof(IClusterNodeFactory), new SingletonServiceFactory(typeof(ClusterNodeFactory)));
            yield return (typeof(IConnectionFactory), new SingletonServiceFactory(typeof(ConnectionFactory)));
            yield return (typeof(IBucketFactory), new SingletonServiceFactory(typeof(BucketFactory)));
            yield return (typeof(IScopeFactory), new SingletonServiceFactory(typeof(ScopeFactory)));
            yield return (typeof(ICollectionFactory), new SingletonServiceFactory(typeof(CollectionFactory)));
            yield return (typeof(IRetryOrchestrator), new SingletonServiceFactory(typeof(RetryOrchestrator)));
            yield return (typeof(IOrphanedResponseLogger),
                new SingletonServiceFactory(typeof(NullOrphanedResponseLogger)));
            yield return (typeof(IVBucketKeyMapperFactory),
                new SingletonServiceFactory(typeof(VBucketKeyMapperFactory)));
            yield return (typeof(IVBucketFactory), new SingletonServiceFactory(typeof(VBucketFactory)));
            yield return (typeof(IKetamaKeyMapperFactory), new SingletonServiceFactory(typeof(KetamaKeyMapperFactory)));
            yield return (typeof(IVBucketServerMapFactory), new SingletonServiceFactory(typeof(VBucketServerMapFactory)));

            yield return (typeof(ITypeSerializer), new SingletonServiceFactory(new DefaultSerializer()));
            yield return (typeof(IDataMapper), new SingletonServiceFactory(typeof(JsonDataMapper)));
            yield return (typeof(ITypeTranscoder), new SingletonServiceFactory(typeof(JsonTranscoder)));

            yield return (typeof(CouchbaseHttpClient), new TransientServiceFactory(typeof(CouchbaseHttpClient)));
            yield return (typeof(IServiceUriProvider), new SingletonServiceFactory(typeof(ServiceUriProvider)));
            yield return (typeof(IConfigHandler), new SingletonServiceFactory(typeof(ConfigHandler)));
            yield return (typeof(IHttpStreamingConfigListenerFactory), new SingletonServiceFactory(typeof(HttpStreamingConfigListenerFactory)));

            yield return (typeof(IAnalyticsClient), new SingletonServiceFactory(typeof(AnalyticsClient)));
            yield return (typeof(ISearchClient), new SingletonServiceFactory(typeof(SearchClient)));
            yield return (typeof(IQueryClient), new SingletonServiceFactory(typeof(QueryClient)));
            yield return (typeof(IViewClient), new SingletonServiceFactory(typeof(ViewClient)));

            yield return (typeof(IBucketManager), new SingletonServiceFactory(typeof(BucketManager)));
            yield return (typeof(IQueryIndexManager), new SingletonServiceFactory(typeof(QueryIndexManager)));
            yield return (typeof(ISearchIndexManager), new SingletonServiceFactory(typeof(SearchIndexManager)));
            yield return (typeof(IUserManager), new SingletonServiceFactory(typeof(UserManager)));

            yield return (typeof(ICircuitBreaker), new SingletonServiceFactory(typeof(CircuitBreaker)));
            yield return (typeof(CircuitBreakerConfiguration),
                new SingletonServiceFactory(typeof(CircuitBreakerConfiguration)));

            yield return (typeof(ISaslMechanismFactory), new SingletonServiceFactory(typeof(SaslMechanismFactory)));
        }
    }
}
