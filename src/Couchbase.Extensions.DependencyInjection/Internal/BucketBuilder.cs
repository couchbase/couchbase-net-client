using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Couchbase.Extensions.DependencyInjection.Internal
{
    /// <summary>
    /// Default implementation of <see cref="IBucketBuilder"/>.
    /// </summary>
    internal class BucketBuilder : IBucketBuilder
    {
        private readonly IServiceCollection _services;
        private readonly Type _bucketProviderType;
        private readonly bool _tryAddMode;

        public BucketBuilder(IServiceCollection services, Type bucketProviderType, bool tryAddMode)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _bucketProviderType = bucketProviderType ?? throw new ArgumentNullException(nameof(bucketProviderType));
            _tryAddMode = tryAddMode;
        }

        /// <inheritdoc />
        public IScopeBuilder AddScope(string scopeName) => new ScopeBuilder(this, scopeName);

        internal void AddCollection(Type collectionProviderType, string scopeName, string collectionName)
        {
            var proxyType =
                NamedCollectionProxyGenerator.Instance.GetProxy(collectionProviderType, _bucketProviderType, scopeName, collectionName);

            if (_tryAddMode)
            {
                _services.TryAddTransient(collectionProviderType, proxyType);
            }
            else
            {
                _services.AddTransient(collectionProviderType, proxyType);
            }
        }
    }
}
