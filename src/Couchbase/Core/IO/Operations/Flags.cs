using System;
using System.Runtime.InteropServices;
using Couchbase.Core.IO.Converters;

namespace Couchbase.Core.IO.Operations
{
    [StructLayout(LayoutKind.Auto)]
    public struct Flags
    {
        public DataFormat DataFormat { get; set; }

        public Compression Compression { get; set; }

        public TypeCode TypeCode { get; set; }

        /// <summary>
        /// Read flags from a buffer. The buffer must be at least 4 bytes long.
        /// </summary>
        /// <param name="buffer">Buffer to read.</param>
        /// <returns>Flags.</returns>
        internal static Flags Read(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length < 4)
            {
                ThrowArgumentException();
            }

            return new Flags
            {
                DataFormat = (DataFormat)(buffer[0] & 0xf), // Lower 4 bits of high byte
                Compression = (Compression)(buffer[0] & 0xe0), // Upper 3 bits of high byte
                TypeCode = (TypeCode)(ByteConverter.ToUInt16(buffer.Slice(2)) & 0xff) // lowest byte
            };
        }

        /// <summary>
        /// Write flags to a buffer. The buffer must be at least 4 bytes long.
        /// </summary>
        /// <param name="buffer">The buffer to receive the flags.</param>
        internal readonly void Write(Span<byte> buffer)
        {
            if (buffer.Length < 4)
            {
                ThrowArgumentException();
            }

            buffer[0] = unchecked((byte)(((int) DataFormat & 0xf) | ((int) Compression & 0xe0)));  // DataFormat is lower 4 bits, compression higher 3 bits
            buffer[1] = 0;
            ByteConverter.FromUInt16((ushort)TypeCode, buffer.Slice(2));
        }

        private static void ThrowArgumentException()
        {
            throw new ArgumentException("buffer must be at least 4 bytes.");
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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

#endregion
