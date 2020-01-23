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
