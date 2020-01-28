using System;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// A factory capable of returning a service.
    /// </summary>
    internal interface IServiceFactory
    {
        /// <summary>
        /// Initializes the factory, making it owned by the given <see cref="IServiceProvider"/>.
        /// </summary>
        /// <param name="serviceProvider">The <seealso cref="IServiceProvider"/>.</param>
        /// <exception cref="ArgumentNullException"></exception>
        void Initialize(IServiceProvider serviceProvider);

        /// <summary>
        /// Creates or returns an existing service.
        /// </summary>
        /// <param name="requestedType">Type being requested.</param>
        /// <returns>The service.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        object? CreateService(Type requestedType);
    }
}
