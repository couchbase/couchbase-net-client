using System;
using Couchbase.Core.Serialization;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;

namespace Couchbase.Core.Transcoders
{
    /// <summary>
    /// An interface for providing transcoder implementations.
    /// </summary>
    public interface ITypeTranscoder
    {
        /// <summary>
        /// Encodes the specified value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The value of the key to encode.</param>
        /// <param name="flags">The flags used for decoding the response.</param>
        /// <returns></returns>
        byte[] Encode<T>(T value, Flags flags);

        /// <summary>
        /// Decodes the specified buffer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer">The buffer representing the value of the key to decode.</param>
        /// <param name="offset">The offset to start reading at.</param>
        /// <param name="length">The length to read from the buffer.</param>
        /// <param name="flags">The flags used to encode the payload.</param>
        /// <returns></returns>
        T Decode<T>(ArraySegment<byte> buffer, int offset, int length, Flags flags);

        /// <summary>
        /// Decodes the specified buffer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <param name="flags">The flags used for decoding the payload.</param>
        /// <returns></returns>
        T Decode<T>(byte[] buffer, int offset, int length, Flags flags);

        /// <summary>
        /// Gets or sets the serializer used by the <see cref="ITypeTranscoder"/> implementation.
        /// </summary>
        ITypeSerializer Serializer { get; set; }

        /// <summary>
        /// Gets or sets the byte converter used by used by the <see cref="ITypeTranscoder"/> implementation.
        /// </summary>
        IByteConverter Converter { get; set; }
    }
}
