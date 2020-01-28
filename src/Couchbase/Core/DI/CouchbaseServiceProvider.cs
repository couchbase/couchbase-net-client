using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Provides a lightweight <seealso cref="IServiceProvider"/> implementation for Couchbase.
    /// </summary>
    /// <remarks>
    /// Supports registering a factory against non-specific generics, i.e. typeof(ILogger&lt;&gt;).
    /// In this example, any request for ILogger&lt;T&gt; will hit that factory, regardless of
    /// the specific T requested, unless a more specific factory is also registered.
    /// </remarks>
    internal class CouchbaseServiceProvider : IServiceProvider
    {
        private readonly IReadOnlyDictionary<Type, IServiceFactory> _services;

        /// <summary>
        /// Create a new CouchbaseServiceProvider.
        /// </summary>
        /// <param name="serviceFactories">Factories keyed by type being requested.</param>
        public CouchbaseServiceProvider(IEnumerable<KeyValuePair<Type, IServiceFactory>> serviceFactories)
        {
            if (serviceFactories == null)
            {
                throw new ArgumentNullException(nameof(serviceFactories));
            }

            var serviceDictionary = serviceFactories.ToDictionary(p => p.Key, p => p.Value);
            serviceDictionary.Add(typeof(IServiceProvider), new SingletonServiceFactory(this));

            _services = new ReadOnlyDictionary<Type, IServiceFactory>(serviceDictionary);

            foreach (var service in _services)
            {
                service.Value.Initialize(this);
            }
        }

        /// <inheritdoc />
        public object? GetService(Type serviceType)
        {
            if (_services.TryGetValue(serviceType, out var factory))
            {
                return factory.CreateService( serviceType);
            }

            if (serviceType.IsGenericType && _services.TryGetValue(serviceType.GetGenericTypeDefinition(), out factory))
            {
                return factory.CreateService(serviceType);
            }

            return null;
        }
    }
}
