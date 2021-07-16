using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// Reads values and objects from a JSON stream asynchronously.
    /// </summary>
    public interface IJsonStreamReader : IDisposable
    {
        /// <summary>
        /// If the reader is stopped on a simple value attribute, returns
        /// the .NET type of the value. Otherwise, returns null.
        /// </summary>
        Type? ValueType { get; }

        /// <summary>
        /// If the reader is stopped on a simple value attribute, returns
        /// the value.
        /// </summary>
        object? Value { get; }

        /// <summary>
        /// Initializes the reader
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><value>true</value> if successfully initialized.</returns>
        /// <exception cref="InvalidOperationException">InitializeAsync should only be called once.</exception>
        Task<bool> InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads until the next attribute is found in the stream. Returns the path to the attribute,
        /// or <value>null</value> if the end of the stream is reached.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// Path to the attribute relative to the overall stream,
        /// or <value>null</value> if the end of the stream is reached"
        /// </returns>
        /// <remarks>
        /// The returned path is "." separated, and relative to the overall stream. For example, if
        /// the attribute "metrics" is on the root object, returns "metrics". If the attribute reached
        /// is "count" on the "metrics" object, the returned value is "metrics.count".
        /// </remarks>
        Task<string?> ReadToNextAttributeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads an object at the current point in the stream.
        /// </summary>
        /// <typeparam name="T">Type of the object to read.</typeparam>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The object read.</returns>
        /// <remarks>
        /// This method also supports reading literals such as strings, numbers, nulls, etc,
        /// given the correct type for <typeparamref name="T"/>.
        /// </remarks>
        Task<T> ReadObjectAsync<T>(CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads an array of tokens at the current point in the stream.
        /// </summary>
        /// <typeparam name="T">Type of elements returned.</typeparam>
        /// <param name="readElement">Function which reads each element of the array.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>An <see cref="IAsyncEnumerable{T}"/> to read the array.</returns>
        IAsyncEnumerable<T> ReadArrayAsync<T>(
            Func<IJsonStreamReader, CancellationToken, Task<T>> readElement,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads a dynamic token at the current point in the stream.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The dynamic object.</returns>
        Task<IJsonToken> ReadTokenAsync(CancellationToken cancellationToken = default);
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
