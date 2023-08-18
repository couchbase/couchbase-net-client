using System;
using System.Diagnostics.CodeAnalysis;

namespace Couchbase.Extensions.DependencyInjection
{
    /// <summary>
    /// Applies additional configuration to a bucket for dependency injection.
    /// </summary>
    public interface IBucketBuilder
    {
        /// <summary>
        /// Begin building a scope with one or more collections.
        /// </summary>
        /// <param name="scopeName">Name of the scope.</param>
        /// <returns>The <see cref="IScopeBuilder"/> for building the scope.</returns>
        IScopeBuilder AddScope(string scopeName);

        /// <summary>
        /// Register a collection via an interface based on <see cref="INamedCollectionProvider"/> and a
        /// concrete implementation of that interface.
        /// </summary>
        /// <typeparam name="TService">Interface inherited from <see cref="INamedCollectionProvider"/>.</typeparam>
        /// <typeparam name="TImplementation">Concrete implementation of <typeparamref name="TService"/>.</typeparam>
        /// <returns>The <see cref="IBucketBuilder"/> for chaining.</returns>
        IBucketBuilder AddCollection<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>()
            where TService : class, INamedCollectionProvider
            where TImplementation : class, TService;
    }
}
