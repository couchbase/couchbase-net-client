using System.Diagnostics.CodeAnalysis;

namespace Couchbase.Extensions.DependencyInjection
{
    /// <summary>
    /// Applies additional configuration to a scope for dependency injection.
    /// </summary>
    public interface IScopeBuilder
    {
        /// <summary>
        /// Register an interface based on <see cref="INamedCollectionProvider"/> which will be injected
        /// with a specific scope and collection name.
        /// </summary>
        /// <typeparam name="T">Interface inherited from <see cref="INamedCollectionProvider"/>. Must not add any members.</typeparam>
        /// <param name="collectionName">Name of the collection.</param>
        /// <returns>The <see cref="IScopeBuilder"/> for chaining.</returns>
        /// <remarks>
        /// This method is not AOT-compatible. Use the overload that accepts a concrete implementation.
        /// </remarks>
        [RequiresDynamicCode(ServiceCollectionExtensions.RequiresDynamicCodeWarning)]
        IScopeBuilder AddCollection<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
            string collectionName)
            where T : class, INamedCollectionProvider;
    }
}
