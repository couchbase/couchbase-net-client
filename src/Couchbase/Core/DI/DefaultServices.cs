using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DataMapping;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Retry;
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

            yield return (typeof(IBucketFactory), new SingletonServiceFactory(typeof(BucketFactory)));
            yield return (typeof(IScopeFactory), new SingletonServiceFactory(typeof(ScopeFactory)));
            yield return (typeof(ICollectionFactory), new SingletonServiceFactory(typeof(CollectionFactory)));
            yield return (typeof(IRetryOrchestrator), new SingletonServiceFactory(typeof(RetryOrchestrator)));

            yield return (typeof(ITypeSerializer), new SingletonServiceFactory(new DefaultSerializer()));
            yield return (typeof(IDataMapper), new SingletonServiceFactory(typeof(JsonDataMapper)));
            yield return (typeof(ITypeTranscoder), new SingletonServiceFactory(typeof(DefaultTranscoder)));

            yield return (typeof(CouchbaseHttpClient), new LambdaServiceFactory(serviceProvider =>
                new CouchbaseHttpClient(serviceProvider.GetRequiredService<ClusterContext>(),
                    serviceProvider.GetRequiredService<ILogger<CouchbaseHttpClient>>())));
            yield return (typeof(ConfigHandler), new SingletonServiceFactory(typeof(ConfigHandler)));
        }
    }
}
