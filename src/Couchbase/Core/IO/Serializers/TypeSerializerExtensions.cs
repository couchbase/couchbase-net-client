using System;

namespace Couchbase.Core.IO.Serializers
{
    public static class TypeSerializerExtensions
    {
        /// <summary>
        /// Deserializes the specified buffer into the <see cref="Type"/> T specified as a generic parameter.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> specified as the type of the value.</typeparam>
        /// <param name="typeSerializer">The <see cref="ITypeSerializer"/>.</param>
        /// <param name="buffer">The buffer to deserialize from.</param>
        /// <param name="offset">The offset of the buffer to start reading from.</param>
        /// <param name="length">The length of the buffer to read from.</param>
        /// <returns>The <see cref="Type"/> instance representing the value of the key.</returns>
        [Obsolete("Use the ReadOnlyMemory<byte> based overload of Deserialize<T>.")]
        public static T Deserialize<T>(this ITypeSerializer typeSerializer, byte[] buffer, int offset, int length)
        {
            return typeSerializer.Deserialize<T>(buffer.AsMemory(offset, length));
        }
    }
}
