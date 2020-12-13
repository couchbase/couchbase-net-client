using System;
using System.Diagnostics.CodeAnalysis;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Extensions for <seealso cref="IServiceProvider"/>.
    /// </summary>
    internal static class CouchbaseServiceProviderExtensions
    {
        /// <summary>
        /// Gets a service, throws an exception if not registered.
        /// </summary>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/>.</param>
        /// <param name="type">Service being requested.</param>
        /// <returns>The service.</returns>
        public static object GetRequiredService(this IServiceProvider serviceProvider, Type type)
        {
            if (serviceProvider == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(serviceProvider));
            }

            var service = serviceProvider.GetService(type);
            if (service == null)
            {
                ThrowHelper.ThrowInvalidOperationException($"Service {type.FullName} is not registered.");
            }

            return service;
        }

        /// <summary>
        /// Gets a service.
        /// </summary>
        /// <typeparam name="T">Service being requested.</typeparam>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/>.</param>
        /// <returns>The service.</returns>
        [return: MaybeNull]
        public static T GetService<T>(this IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(serviceProvider));
            }

            return (T) serviceProvider.GetService(typeof(T));
        }

        /// <summary>
        /// Gets a service, throws an exception if not registered.
        /// </summary>
        /// <typeparam name="T">Service being requested.</typeparam>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/>.</param>
        /// <returns>The service.</returns>
        public static T GetRequiredService<T>(this IServiceProvider serviceProvider) =>
            (T) serviceProvider.GetRequiredService(typeof(T));
    }
}
