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
        public static T? GetService<T>(this IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(serviceProvider));
            }

            return (T?) serviceProvider.GetService(typeof(T));
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


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
