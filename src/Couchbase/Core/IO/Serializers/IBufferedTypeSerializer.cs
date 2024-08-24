using System;
using System.Buffers;

#nullable enable

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// Extension of <see cref="ITypeSerializer"/> which can read from <see cref="ReadOnlySequence{Byte}"/>
    /// and write to <see cref="IBufferWriter{Byte}"/>.
    /// </summary>
    public interface IBufferedTypeSerializer : ITypeSerializer
    {
        /// <summary>
        /// Deserializes the specified buffer into the type specified by <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="buffer">The buffer to deserialize from.</param>
        /// <returns>The deserialized value.</returns>
        T? Deserialize<T>(ReadOnlySequence<byte> buffer);

        /// <summary>
        /// Serializes the specified object onto an <see cref="IBufferWriter{T}"/>.
        /// </summary>
        /// <typeparam name="T">Type of object to serialize.</typeparam>
        /// <param name="writer">The writer to receive the serialized object.</param>
        /// <param name="obj">The object to serialize.</param>
        void Serialize<T>(IBufferWriter<byte> writer, T obj);

        /// <summary>
        /// Determines if the serializer can serialize and deserialize the specified type.
        /// </summary>
        /// <param name="type">Type of object to serialize or deserialize.</param>
        /// <returns><c>true</c> if the type can be serialized and deserialized.</returns>
        bool CanSerialize(Type type);
    }
}
