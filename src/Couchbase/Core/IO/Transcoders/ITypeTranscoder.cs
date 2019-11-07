using System;
using System.IO;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;

namespace Couchbase.Core.IO.Transcoders
{
    /// <summary>
    /// An interface for providing transcoder implementations.
    /// </summary>
    public interface ITypeTranscoder
    {
        /// <summary>
        /// Get data formatting based on the generic type and/or the actual value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">Value to be formatted.</param>
        /// <returns>Flags used to format value written to operation payload.</returns>
        Flags GetFormat<T>(T value);

        /// <summary>
        /// Encodes the specified value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="stream">The stream to receive the encoded value.</param>
        /// <param name="value">The value of the key to encode.</param>
        /// <param name="flags">The flags used for decoding the response.</param>
        /// <param name="opcode"></param>
        void Encode<T>(Stream stream, T value, Flags flags, OpCode opcode);

        /// <summary>
        /// Decodes the specified buffer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="buffer">The buffer representing the value of the key to decode.</param>
        /// <param name="flags">The flags used to encode the payload.</param>
        /// <param name="opcode"></param>
        /// <returns></returns>
        T Decode<T>(ReadOnlyMemory<byte> buffer, Flags flags, OpCode opcode);

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
