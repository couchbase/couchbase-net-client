using System;

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
        public static IScopeBuilder AddDefaultCollection<T>(this IBucketBuilder builder)
            where T : class, INamedCollectionProvider =>
            builder.AddDefaultScope().AddDefaultCollection<T>();

        /// <summary>
        /// Begin building the default scope.
        /// </summary>
        /// <param name="builder">The bucket builder.</param>
        /// <returns>The <see cref="IScopeBuilder"/> for building the scope.</returns>
        public static IScopeBuilder AddDefaultScope(this IBucketBuilder builder) =>
            builder.AddScope("_default");

        /// <summary>
        /// Register an interface based on <see cref="INamedCollectionProvider"/> which will be injected
        /// with the default scope/collection.
        /// </summary>
        /// <typeparam name="T">Interface inherited from <see cref="INamedCollectionProvider"/>. Must not add any members.</typeparam>
        /// <param name="builder">The scope builder.</param>
        /// <returns>The <see cref="IScopeBuilder"/> for chaining.</returns>
        private static IScopeBuilder AddDefaultCollection<T>(this IScopeBuilder builder)
            where T : class, INamedCollectionProvider =>
            builder.AddCollection<T>("_default");
    }
}
