using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// Provides an interface for serialization and deserialization of K/V pairs.
    /// </summary>
    public interface ITypeSerializer
    {
        /// <summary>
        /// Deserializes the specified buffer into the <see cref="Type"/> T specified as a generic parameter.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> specified as the type of the value.</typeparam>
        /// <param name="buffer">The buffer to deserialize from.</param>
        /// <returns>The <see cref="Type"/> instance representing the value of the key.</returns>
        T Deserialize<T>(ReadOnlyMemory<byte> buffer);

        /// <summary>
        /// Deserializes the specified stream into the <see cref="Type"/> T specified as a generic parameter.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> specified as the type of the value.</typeparam>
        /// <param name="stream">The stream.</param>
        /// <returns>The <see cref="Type"/> instance representing the value of the key.</returns>
        T Deserialize<T>(Stream stream);

        /// <summary>
        /// Deserializes the specified stream into the <see cref="Type"/> T specified as a generic parameter.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> specified as the type of the value.</typeparam>
        /// <param name="stream">The stream.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The <see cref="Type"/> instance representing the value of the key.</returns>
        ValueTask<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default);

        /// <summary>
        /// Serializes the specified object onto a stream.
        /// </summary>
        /// <param name="stream">The stream to receive the serialized object.</param>
        /// <param name="obj">The object to serialize.</param>
        void Serialize(Stream stream, object obj);

        /// <summary>
        /// Serializes the specified object onto a stream.
        /// </summary>
        /// <param name="stream">The stream to receive the serialized object.</param>
        /// <param name="obj">The object to serialize.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        ValueTask SerializeAsync(Stream stream, object obj, CancellationToken cancellationToken = default);
    }
}
