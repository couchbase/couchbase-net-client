using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Analytics;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.CircuitBreakers;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Configuration.Server.Streaming;
using Couchbase.Core.DataMapping;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Compression;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Core.Version;
using Couchbase.Management.Analytics;
using Couchbase.Management.Buckets;
using Couchbase.Management.Collections;
using Couchbase.Management.Eventing;
using Couchbase.Management.Eventing.Internal;
using Couchbase.Management.Query;
using Couchbase.Management.Search;
using Couchbase.Management.Users;
using Couchbase.Management.Views;
using Couchbase.Query;
using Couchbase.Search;
using Couchbase.Utils;
using Couchbase.Views;
using DnsClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.ObjectPool;

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
            yield return (typeof(IRedactor), new SingletonServiceFactory(typeof(Redactor)));
            yield return (typeof(TypedRedactor), new SingletonServiceFactory(typeof(TypedRedactor)));
            yield return (typeof(IRequestTracer), new SingletonServiceFactory(NoopRequestTracer.Instance));

            yield return (typeof(ILookupClient), new TransientServiceFactory(_ => new LookupClient()));
            yield return (typeof(IDotNetDnsClient), new TransientServiceFactory(_ => new DotNetDnsClient()));
            yield return (typeof(IDnsResolver), new SingletonServiceFactory(serviceProvider =>
            {
                var options = serviceProvider.GetRequiredService<ClusterOptions>();

                return new DnsClientDnsResolver(
                    serviceProvider.GetRequiredService<ILookupClient>(),
                    serviceProvider.GetRequiredService<IDotNetDnsClient>(),
                    serviceProvider.GetRequiredService<ILogger<DnsClientDnsResolver>>())
                {
                    IpAddressMode = options.ForceIPv4 ? IpAddressMode.ForceIpv4 : IpAddressMode.Default,
                    EnableDnsSrvResolution = options.EnableDnsSrvResolution
                };
            }));
            yield return (typeof(IIpEndPointService), new TransientServiceFactory(typeof(IpEndPointService)));

            yield return (typeof(IClusterNodeFactory), new SingletonServiceFactory(typeof(ClusterNodeFactory)));
            yield return (typeof(IConnectionFactory), new SingletonServiceFactory(typeof(ConnectionFactory)));
            yield return (typeof(IConnectionPoolFactory), new SingletonServiceFactory(typeof(ConnectionPoolFactory)));
            yield return (typeof(IConnectionPoolScaleControllerFactory),
                new SingletonServiceFactory(typeof(ConnectionPoolScaleControllerFactory)));

            yield return (typeof(IBucketFactory), new SingletonServiceFactory(typeof(BucketFactory)));
            yield return (typeof(IScopeFactory), new SingletonServiceFactory(typeof(ScopeFactory)));
            yield return (typeof(ICollectionFactory), new SingletonServiceFactory(typeof(CollectionFactory)));
            yield return (typeof(IRetryOrchestrator), new SingletonServiceFactory(typeof(RetryOrchestrator)));
            yield return (typeof(IVBucketKeyMapperFactory), new SingletonServiceFactory(typeof(VBucketKeyMapperFactory)));
            yield return (typeof(IVBucketFactory), new SingletonServiceFactory(typeof(VBucketFactory)));
            yield return (typeof(IKetamaKeyMapperFactory), new SingletonServiceFactory(typeof(KetamaKeyMapperFactory)));
            yield return (typeof(IOperationConfigurator), new SingletonServiceFactory(typeof(OperationConfigurator)));
            yield return (typeof(IOperationCompressor), new SingletonServiceFactory(typeof(OperationCompressor)));
            yield return (typeof(ICompressionAlgorithm), new SingletonServiceFactory(typeof(NullCompressionAlgorithm)));

            yield return (typeof(ITypeSerializer), new SingletonServiceFactory(DefaultSerializer.Instance));
            yield return (typeof(IFallbackTypeSerializerProvider), new SingletonServiceFactory(DefaultFallbackTypeSerializerProvider.Instance));
            yield return (typeof(IDataMapper), new SingletonServiceFactory(typeof(JsonDataMapper)));
            yield return (typeof(ITypeTranscoder), new SingletonServiceFactory(
                // Using the lambda approach avoids rooting all constructors of JsonTranscoder by SingletonServiceFactory
                serviceProvider => new JsonTranscoder(serviceProvider.GetRequiredService<ITypeSerializer>())));

            yield return (typeof(ICouchbaseHttpClientFactory), new SingletonServiceFactory(typeof(CouchbaseHttpClientFactory)));
            yield return (typeof(IServiceUriProvider), new SingletonServiceFactory(typeof(ServiceUriProvider)));
            yield return (typeof(IConfigHandler), new SingletonServiceFactory(typeof(ConfigHandler)));
            yield return (typeof(IHttpStreamingConfigListenerFactory), new SingletonServiceFactory(typeof(HttpStreamingConfigListenerFactory)));

            yield return (typeof(IAnalyticsClient), new SingletonServiceFactory(typeof(AnalyticsClient)));
            yield return (typeof(ISearchClient), new SingletonServiceFactory(typeof(SearchClient)));
            yield return (typeof(IQueryClient), new SingletonServiceFactory(typeof(QueryClient)));
            yield return (typeof(IViewClient), new SingletonServiceFactory(typeof(ViewClient)));
            yield return (typeof(IEventingFunctionService), new SingletonServiceFactory(typeof(EventingFunctionService)));

            yield return (typeof(IBucketManager), new SingletonServiceFactory(typeof(BucketManager)));
            yield return (typeof(ICollectionManagerFactory), new SingletonServiceFactory(typeof(CollectionManagerFactory)));
            yield return (typeof(IQueryIndexManager), new SingletonServiceFactory(typeof(QueryIndexManager)));
            yield return (typeof(ICollectionQueryIndexManagerFactory), new SingletonServiceFactory(typeof(CollectionQueryIndexManagerFactory)));
            yield return (typeof(IViewIndexManagerFactory), new SingletonServiceFactory(typeof(ViewIndexManagerFactory)));
            yield return (typeof(ISearchIndexManager), new SingletonServiceFactory(typeof(SearchIndexManager)));
            yield return (typeof(IUserManager), new SingletonServiceFactory(typeof(UserManager)));
            yield return (typeof(IAnalyticsIndexManager), new SingletonServiceFactory(typeof(AnalyticsIndexManager)));
            yield return (typeof(IEventingFunctionManagerFactory), new SingletonServiceFactory(typeof(EventingFunctionManagerFactory)));

            yield return (typeof(ICircuitBreaker), new SingletonServiceFactory(typeof(CircuitBreaker)));
            yield return (typeof(CircuitBreakerConfiguration), new SingletonServiceFactory(typeof(CircuitBreakerConfiguration)));

            yield return (typeof(ISaslMechanismFactory), new SingletonServiceFactory(typeof(SaslMechanismFactory)));
            yield return (typeof(IBootstrapperFactory), new SingletonServiceFactory(typeof(BootstrapperFactory)));
            yield return (typeof(IClusterVersionProvider), new SingletonServiceFactory(typeof(ClusterVersionProvider)));

            yield return (typeof(ObjectPoolProvider), new SingletonServiceFactory(serviceProvider => new DefaultObjectPoolProvider
            {
                MaximumRetained = serviceProvider.GetRequiredService<ClusterOptions>().Tuning.MaximumRetainedOperationBuilders
            }));
            yield return (typeof(ObjectPool<OperationBuilder>), new SingletonServiceFactory(serviceProvider =>
                serviceProvider.GetRequiredService<ObjectPoolProvider>().Create(
                    new OperationBuilderPoolPolicy
                    {
                        MaximumOperationBuilderCapacity = serviceProvider.GetRequiredService<ClusterOptions>()
                            .Tuning.MaximumOperationBuilderCapacity
                    })));
            yield return (typeof(IHttpClusterMapFactory), new SingletonServiceFactory(typeof(HttpClusterMapFactory)));
            yield return (typeof(ICollectionQueryIndexManager), new SingletonServiceFactory(typeof(CollectionQueryIndexManager)));
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
