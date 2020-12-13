using System;
using Couchbase.Utils;

namespace Couchbase.Core.Utils
{
    internal static class Leb128
    {
        /// <summary>
        /// Maximum length, in bytes, when encoding a 32-bit integer.
        /// </summary>
        public const int MaxLength = 5;

        /// <summary>
        /// Encodes a value onto a buffer using LEB128 encoding.
        /// </summary>
        /// <param name="buffer">Buffer to receive the value.</param>
        /// <param name="value">Value to encode.</param>
        /// <returns>Number of bytes encoded.</returns>
        public static int Write(Span<byte> buffer, uint value)
        {
            var index = 0;

            while (true)
            {
                // get next 7 lower bits
                var @byte = (byte) (value & 0x7f);
                value >>= 7;

                if (value == 0) // this was the last byte
                {
                    buffer[index++] = @byte;
                    return index;
                }

                buffer[index++] = (byte)(@byte | 0x80);
            }
        }

        public static uint Read(ReadOnlySpan<byte> bytes)
        {
            var result = 0u;
            uint current;
            var count = 0;

            do
            {
                current = (uint) bytes[count] & 0xff;
                result |= (current & 0x7f) << (count * 7);
                count++;
            } while ((current & 0x80) == 0x80 && count < 5);

            if ((current & 0x80) == 0x80)
            {
                ThrowHelper.ThrowInvalidOperationException("Invalid LEB128 sequence.");
            }
            return result;
        }

        public static int WrittenSize(uint value)
        {
            var remaining = value >> 7;
            var count = 0;

            while (remaining != 0)
            {
                remaining >>= 7;
                count++;
            }
            return count + 1;
        }
    }
}
