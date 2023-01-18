using System;
using System.Text;
using Couchbase.Core.IO.Converters;
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

        public static (long Value, short Length) Read(ReadOnlySpan<byte> bytes)
        {
            long result = 0;
            long current;
            short count = 0;

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
            return (result, count);
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
