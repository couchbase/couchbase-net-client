using System;   
using System.IO;

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
        /// <param name="offset">The offset of the buffer to start reading from.</param>
        /// <param name="length">The length of the buffer to read from.</param>
        /// <returns>The <see cref="Type"/> instance representing the value of the key.</returns>
        T Deserialize<T>(byte[] buffer, int offset, int length);

        /// <summary>
        /// Deserializes the specified stream into the <see cref="Type"/> T specified as a generic parameter.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> specified as the type of the value.</typeparam>
        /// <param name="stream">The stream.</param>
        /// <returns>The <see cref="Type"/> instance representing the value of the key.</returns>
        T Deserialize<T>(Stream stream);

        /// <summary>
        /// Serializes the specified object into a buffer.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>A <see cref="byte"/> array that is the serialized value of the key.</returns>
        byte[] Serialize(object obj);
    }
}
