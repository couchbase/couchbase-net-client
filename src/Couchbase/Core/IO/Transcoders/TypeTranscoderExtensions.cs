using System;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Legacy;

namespace Couchbase.Core.IO.Transcoders
{
    public static class TypeTranscoderExtensions
    {
        /// <summary>
        /// Decodes the specified buffer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="typeTranscoder">The <see cref="ITypeTranscoder"/>.</param>
        /// <param name="buffer">The buffer representing the value of the key to decode.</param>
        /// <param name="offset">The offset to start reading at.</param>
        /// <param name="length">The length to read from the buffer.</param>
        /// <param name="flags">The flags used to encode the payload.</param>
        /// <param name="opcode"></param>
        /// <returns></returns>
        [Obsolete("Use the ReadOnlyMemory<byte> based overload of Decode<T>.")]
        public static T Decode<T>(this ITypeTranscoder typeTranscoder, ArraySegment<byte> buffer, int offset,
            int length, Flags flags, OpCode opcode)
        {
            return typeTranscoder.Decode<T>(buffer.AsMemory(offset, length), flags, opcode);
        }

        /// <summary>
        /// Decodes the specified buffer.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="typeTranscoder">The <see cref="ITypeTranscoder"/>.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <param name="flags">The flags used for decoding the payload.</param>
        /// <param name="opcode"></param>
        /// <returns></returns>
        [Obsolete("Use the ReadOnlyMemory<byte> based overload of Decode<T>.")]
        public static T Decode<T>(this ITypeTranscoder typeTranscoder, byte[] buffer, int offset,
            int length, Flags flags, OpCode opcode)
        {
            return typeTranscoder.Decode<T>(buffer.AsMemory(offset, length), flags, opcode);
        }
    }
}
