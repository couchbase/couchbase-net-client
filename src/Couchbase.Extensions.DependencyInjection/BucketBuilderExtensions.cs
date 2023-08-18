using System.Diagnostics.CodeAnalysis;

namespace Couchbase.Extensions.DependencyInjection
{
    /// <summary>
    /// Extensions for <see cref="IBucketBuilder"/> and <see cref="IScopeBuilder"/>.
    /// </summary>
    public static class BucketBuilderExtensions
    {
        /// <summary>
        /// Register an interface based on <see cref="INamedCollectionProvider"/> which will be injected
        /// with the default scope/collection.
        /// </summary>
        /// <typeparam name="T">Interface inherited from <see cref="INamedCollectionProvider"/>. Must not add any members.</typeparam>
        /// <param name="builder">The bucket builder.</param>
        /// <returns>The <see cref="IScopeBuilder"/> for the default scope, used for chaining.</returns>
        /// <remarks>
        /// This method is not AOT-compatible. Use the overload that accepts a concrete implementation.
        /// </remarks>
        [RequiresDynamicCode(ServiceCollectionExtensions.RequiresDynamicCodeWarning)]
        public static IScopeBuilder AddDefaultCollection<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
            this IBucketBuilder builder)
            where T : class, INamedCollectionProvider =>
            builder.AddDefaultScope().AddDefaultCollection<T>();

        /// <summary>
        /// Begin building the default scope.
        /// </summary>
        /// <param name="builder">The bucket builder.</param>
        /// <returns>The <see cref="IScopeBuilder"/> for building the scope.</returns>
        public static IScopeBuilder AddDefaultScope(this IBucketBuilder builder) =>
            builder.AddScope(NamedCollectionProvider.DefaultScopeName);

        /// <summary>
        /// Register an interface based on <see cref="INamedCollectionProvider"/> which will be injected
        /// with the default scope/collection.
        /// </summary>
        /// <typeparam name="T">Interface inherited from <see cref="INamedCollectionProvider"/>. Must not add any members.</typeparam>
        /// <param name="builder">The scope builder.</param>
        /// <returns>The <see cref="IScopeBuilder"/> for chaining.</returns>
        /// <remarks>
        /// This method is not AOT-compatible. Use the overload that accepts a concrete implementation.
        /// </remarks>
        [RequiresDynamicCode(ServiceCollectionExtensions.RequiresDynamicCodeWarning)]
        private static IScopeBuilder AddDefaultCollection<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
            this IScopeBuilder builder)
            where T : class, INamedCollectionProvider =>
            builder.AddCollection<T>(NamedCollectionProvider.DefaultCollectionName);
    }
}
