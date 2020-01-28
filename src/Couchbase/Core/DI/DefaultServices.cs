using System;
using System.Collections.Generic;
using System.Linq;
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
        }
    }
}
