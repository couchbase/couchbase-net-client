using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// Extensions for <seealso cref="IJsonStreamReader"/>.
    /// </summary>
    public static class JsonStreamDeserializerExtensions
    {
        /// <summary>
        /// Read an array at the current point in the stream as an array of <seealso cref="IJsonToken"/>.
        /// </summary>
        /// <param name="reader">The <seealso cref="IJsonStreamReader"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An <see cref="IAsyncEnumerable{T}"/> of the tokens.</returns>
        public static IAsyncEnumerable<IJsonToken> ReadTokensAsync(this IJsonStreamReader reader,
            CancellationToken cancellationToken = default) =>
            reader.ReadArrayAsync(ReadTokenElement, cancellationToken);

        /// <summary>
        /// Read an array at the current point in the stream as an array of POCOs.
        /// </summary>
        /// <typeparam name="T">Type of POCO in the array.</typeparam>
        /// <param name="reader">The <seealso cref="IJsonStreamReader"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An <see cref="IAsyncEnumerable{T}"/> of the objects.</returns>
        public static IAsyncEnumerable<T> ReadObjectsAsync<T>(this IJsonStreamReader reader,
            CancellationToken cancellationToken = default) =>
            reader.ReadArrayAsync(ReadObjectElement<T>, cancellationToken);

        private static Task<IJsonToken> ReadTokenElement(IJsonStreamReader reader,
            CancellationToken cancellationToken) =>
            reader.ReadTokenAsync(cancellationToken);

        private static Task<T> ReadObjectElement<T>(IJsonStreamReader reader,
            CancellationToken cancellationToken) =>
            reader.ReadObjectAsync<T>(cancellationToken);
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
