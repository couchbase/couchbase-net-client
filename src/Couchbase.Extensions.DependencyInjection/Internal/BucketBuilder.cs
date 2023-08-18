using System;
using System.Diagnostics.CodeAnalysis;
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

        [RequiresDynamicCode(ServiceCollectionExtensions.RequiresDynamicCodeWarning)]
        internal void AddCollection([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type collectionProviderType,
            string scopeName, string collectionName)
        {
            var proxyType =
                NamedCollectionProxyGenerator.Instance.GetProxy(collectionProviderType, _bucketProviderType, scopeName, collectionName);

            AddCollection(collectionProviderType, proxyType);
        }

        /// <inheritdoc />
        public IBucketBuilder AddCollection<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
            where TService : class, INamedCollectionProvider
            where TImplementation : class, TService
        {
            AddCollection(typeof(TService), typeof(TImplementation));

            return this;
        }

        internal void AddCollection(Type collectionProviderType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type concreteType)
        {
            if (_tryAddMode)
            {
                _services.TryAddTransient(collectionProviderType, concreteType);
            }
            else
            {
                _services.AddTransient(collectionProviderType, concreteType);
            }
        }
    }
}
