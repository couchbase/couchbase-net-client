using System;
using System.Diagnostics.CodeAnalysis;

namespace Couchbase.Extensions.DependencyInjection.Internal
{
    internal class ScopeBuilder : IScopeBuilder
    {
        private readonly BucketBuilder _bucketBuilder;
        private readonly string _scopeName;

        public ScopeBuilder(BucketBuilder bucketBuilder, string scopeName)
        {
            if (string.IsNullOrEmpty(scopeName))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(scopeName));
            }

            _bucketBuilder = bucketBuilder ?? throw new ArgumentNullException(nameof(bucketBuilder));
            _scopeName = scopeName;
        }

        /// <inheritdoc />
        [RequiresDynamicCode(ServiceCollectionExtensions.RequiresDynamicCodeWarning)]
        public IScopeBuilder AddCollection<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
            string collectionName)
            where T : class, INamedCollectionProvider
        {
            if (string.IsNullOrEmpty(collectionName))
            {
                throw new ArgumentException("Value cannot be null or empty.", nameof(collectionName));
            }

            _bucketBuilder.AddCollection(typeof(T), _scopeName, collectionName);

            return this;
        }
    }
}
