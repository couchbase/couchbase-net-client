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
