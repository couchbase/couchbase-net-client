using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Views;

namespace Couchbase.Core.DataMapping
{
    /// <summary>
    /// Provides and interface for mapping the results of a <see cref="ViewQuery"/> to it's <see cref="IViewResult{TKey, TValue}"/>
    /// </summary>
    public interface IDataMapper
    {
        /// <summary>
        /// Maps the entire results
        /// </summary>
        /// <typeparam name="T">The <see cref="IViewResult{TKey, TValue}"/>'s Type parameter.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> results of the query.</param>
        /// <returns>An object deserialized to it's T type.</returns>
        T Map<T>(Stream stream) where T : class;

        /// <summary>
        /// Maps the entire results
        /// </summary>
        /// <typeparam name="T">The <see cref="IViewResult{TKey, TValue}"/>'s Type parameter.</typeparam>
        /// <param name="stream">The <see cref="Stream"/> results of the query.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An object deserialized to it's T type.</returns>
        ValueTask<T> MapAsync<T>(Stream stream, CancellationToken cancellationToken = default) where T : class;
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
