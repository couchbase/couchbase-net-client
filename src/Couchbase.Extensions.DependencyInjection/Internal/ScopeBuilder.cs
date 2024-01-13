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
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (bucketBuilder == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(bucketBuilder));
            }
            if (string.IsNullOrEmpty(scopeName))
            {
                ThrowHelper.ThrowArgumentException("Value cannot be null or empty.", nameof(scopeName));
            }

            _bucketBuilder = bucketBuilder;
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
                ThrowHelper.ThrowArgumentException("Value cannot be null or empty.", nameof(collectionName));
            }

            _bucketBuilder.AddCollection(typeof(T), _scopeName, collectionName);

            return this;
        }
    }
}
