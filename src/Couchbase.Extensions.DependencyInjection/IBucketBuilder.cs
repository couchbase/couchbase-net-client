using System;

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
    }
}
