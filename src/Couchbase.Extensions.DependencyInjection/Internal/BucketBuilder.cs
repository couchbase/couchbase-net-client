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
        private readonly string? _serviceKey;
        private readonly bool _tryAddMode;

        public BucketBuilder(IServiceCollection services,
            Type bucketProviderType,
            string? serviceKey,
            bool tryAddMode)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (services == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(services));
            }
            if (bucketProviderType == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(bucketProviderType));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

            _services = services;
            _bucketProviderType = bucketProviderType;
            _serviceKey = serviceKey;
            _tryAddMode = tryAddMode;
        }

        /// <inheritdoc />
        public IScopeBuilder AddScope(string scopeName) => new ScopeBuilder(this, scopeName);

        [RequiresDynamicCode(ServiceCollectionExtensions.RequiresDynamicCodeWarning)]
        internal void AddCollection([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type collectionProviderType,
            string scopeName, string collectionName)
        {
            var proxyType =
                NamedCollectionProxyGenerator.Instance.GetProxy(collectionProviderType, _bucketProviderType, _serviceKey, scopeName, collectionName);

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
                _services.TryAddKeyedTransient(collectionProviderType, _serviceKey, concreteType);
            }
            else
            {
                _services.AddKeyedTransient(collectionProviderType, _serviceKey, concreteType);
            }
        }
    }
}
