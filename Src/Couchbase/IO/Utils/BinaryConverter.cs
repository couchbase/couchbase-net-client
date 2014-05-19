using System;
using System.Text;

namespace Couchbase.IO.Utils
{
    /// <summary>
    /// Provides helper methods for converting between binary and CLR types and vice-versa.
    /// </summary>
    internal static class BinaryConverter
    {
        /// <summary>
        /// Decodes a segment of a buffer int a <see cref="UInt16"/> given an offset.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="offset">The offset to start from.</param>
        /// <returns>A <see cref="UInt16"/> value.</returns>
        public static ushort DecodeUInt16(byte[] buffer, int offset)
        {
            return (ushort)((buffer[offset] << 8) + buffer[offset + 1]);
        }

        /// <summary>
        /// Decodes a segment of a buffer int a <see cref="UInt16"/> given an offset.
        /// </summary>
        /// <param name="buffer">A pointer to a buffer to read from.</param>
        /// <param name="offset">The offset to start from.</param>
        /// <returns>A <see cref="UInt16"/> value.</returns>
        public static unsafe ushort DecodeUInt16(byte* buffer, int offset)
        {
            return (ushort)((buffer[offset] << 8) + buffer[offset + 1]);
        }

        /// <summary>
        /// Decodes a segment of a buffer int a <see cref="Int32"/> given an offset.
        /// </summary>
        /// <param name="segment">A <see cref="ArraySegment{T}"/> where T is byte to read from.</param>
        /// <param name="offset">The offset to start from.</param>
        /// <returns>A <see cref="Int32"/> value.</returns>
        public static unsafe int DecodeInt32(ArraySegment<byte> segment, int offset)
        {
            fixed (byte* buffer = segment.Array)
            {
                return DecodeInt32(buffer, 0);
            }
        }

        /// <summary>
        /// Decodes a segment of a buffer int a <see cref="Int32"/> given an offset.
        /// </summary>
        /// <param name="buffer">A pointer to a buffer to read from.</param>
        /// <param name="offset">The offset to start from.</param>
        /// <returns>A <see cref="Int32"/> value.</returns>
        public static unsafe int DecodeInt32(byte* buffer, int offset)
        {
            buffer += offset;

            return (buffer[0] << 24) | (buffer[1] << 16) | (buffer[2] << 8) | buffer[3];
        }

        /// <summary>
        /// Decodes a segment of a buffer int a <see cref="Int32"/> given an offset.
        /// </summary>
        /// <param name="buffer">A buffer to read from.</param>
        /// <param name="offset">The offset to start from.</param>
        /// <returns>A <see cref="Int32"/> value.</returns>
        public static int DecodeInt32(byte[] buffer, int offset)
        {
            return (buffer[offset] << 24) | (buffer[offset + 1] << 16) | (buffer[offset + 2] << 8) | buffer[offset + 3];
        }

        /// <summary>
        /// Decodes a segment of a buffer int a <see cref="UInt32"/> given an offset.
        /// </summary>
        /// <param name="buffer">A pointer to a buffer to read from.</param>
        /// <param name="offset">The offset to start from.</param>
        /// <returns>A <see cref="UInt32"/> value.</returns>
        public static uint DecodeUInt32(byte[] buffer, int offset)
        {
            return (uint)DecodeInt32(buffer, offset);
        }

        /// <summary>
        /// Decodes a segment of a buffer int a <see cref="UInt64"/> given an offset.
        /// </summary>
        /// <param name="buffer">A buffer to read from.</param>
        /// <param name="offset">The offset to start from.</param>
        /// <returns>A <see cref="UInt64"/> value.</returns>
        public static unsafe ulong DecodeUInt64(byte[] buffer, int offset)
        {
            fixed (byte* ptr = buffer)
            {
                return DecodeUInt64(ptr, offset);
            }
        }

        /// <summary>
        /// Decodes a segment of a buffer int a <see cref="UInt64"/> given an offset.
        /// </summary>
        /// <param name="buffer">A pointer to a buffer to read from.</param>
        /// <param name="offset">The offset to start from.</param>
        /// <returns>A <see cref="UInt64"/> value.</returns>
        public static unsafe ulong DecodeUInt64(byte* buffer, int offset)
        {
            buffer += offset;
            var part1 = (uint)((buffer[0] << 24) | (buffer[1] << 16) | (buffer[2] << 8) | buffer[3]);
            var part2 = (uint)((buffer[4] << 24) | (buffer[5] << 16) | (buffer[6] << 8) | buffer[7]);
            return ((ulong)part1 << 32) | part2;
        }

        /// <summary>
        /// Encodes a <see cref="UInt16"/> value into a buffer.
        /// </summary>
        /// <param name="value">The <see cref="UInt16"/> value to write.</param>
        /// <param name="buffer">The buffer the value will be written to.</param>
        /// <param name="offset">The offset to start at.</param>
        public static unsafe void EncodeUInt16(uint value, byte[] buffer, int offset)
        {
            fixed (byte* bufferPtr = buffer)
            {
                EncodeUInt16(value, bufferPtr, offset);
            }
        }

        /// <summary>
        /// Encodes a <see cref="UInt16"/> value into a to buffer.
        /// </summary>
        /// <param name="value">The <see cref="UInt16"/> value to write.</param>
        /// <param name="buffer">The pointer to the buffer the value will be written to.</param>
        /// <param name="offset">The offset to start at.</param>
        public static unsafe void EncodeUInt16(uint value, byte* buffer, int offset)
        {
            byte* ptr = buffer + offset;
            ptr[0] = (byte)(value >> 8);
            ptr[1] = (byte)(value & 255);
        }

        /// <summary>
        /// Encodes a <see cref="UInt32"/> value into a buffer.
        /// </summary>
        /// <param name="value">The <see cref="UInt32"/> value to write.</param>
        /// <param name="buffer">The buffer the value will be written to.</param>
        /// <param name="offset">The offset to start at.</param>
        public static unsafe void EncodeUInt32(uint value, byte[] buffer, int offset)
        {
            fixed (byte* bufferPtr = buffer)
            {
                EncodeUInt32(value, bufferPtr, offset);
            }
        }

        /// <summary>
        /// Encodes a <see cref="UInt16"/> value into a buffer.
        /// </summary>
        /// <param name="value">The <see cref="UInt16"/> value to write.</param>
        /// <param name="buffer">The pointer to the buffer the value will be written to.</param>
        /// <param name="offset">The offset to start at.</param>
        public static unsafe void EncodeUInt32(uint value, byte* buffer, int offset)
        {
            byte* ptr = buffer + offset;
            ptr[0] = (byte)(value >> 24);
            ptr[1] = (byte)(value >> 16);
            ptr[2] = (byte)(value >> 8);
            ptr[3] = (byte)(value & 255);
        }

        /// <summary>
        /// Encodes a <see cref="UInt64"/> value into a buffer.
        /// </summary>
        /// <param name="value">The <see cref="UInt64"/> value to write.</param>
        /// <param name="buffer">The buffer the value will be written to.</param>
        /// <param name="offset">The offset to start at.</param>
        public static unsafe void EncodeUInt64(ulong value, byte[] buffer, int offset)
        {
            fixed (byte* bufferPtr = buffer)
            {
                EncodeUInt64(value, bufferPtr, offset);
            }
        }

        /// <summary>
        /// Encodes a <see cref="UInt64"/> value into a buffer.
        /// </summary>
        /// <param name="value">The <see cref="UInt64"/> value to write.</param>
        /// <param name="buffer">The pointer to the buffer the value will be written to.</param>
        /// <param name="offset">The offset to start at.</param>
        public static unsafe void EncodeUInt64(ulong value, byte* buffer, int offset)
        {
            byte* ptr = buffer + offset;
            ptr[0] = (byte)(value >> 56);
            ptr[1] = (byte)(value >> 48);
            ptr[2] = (byte)(value >> 40);
            ptr[3] = (byte)(value >> 32);
            ptr[4] = (byte)(value >> 24);
            ptr[5] = (byte)(value >> 16);
            ptr[6] = (byte)(value >> 8);
            ptr[7] = (byte)(value & 255);
        }

        /// <summary>
        /// Encodes a <see cref="String"/> key to a byte array.
        /// </summary>
        /// <param name="key">The key to encode.</param>
        /// <returns>An array of bytes.</returns>
        public static byte[] EncodeKey(string key)
        {
            return String.IsNullOrEmpty(key) ? null : Encoding.UTF8.GetBytes(key);
        }

        /// <summary>
        /// Decodes a byte array into a <see cref="String"/> key.
        /// </summary>
        /// <param name="data">The bte array to decode into a string key.</param>
        /// <returns>A <see cref="String"/> representation of the byte array.</returns>
        public static string DecodeKey(byte[] data)
        {
            if (data == null || data.Length == 0) return null;

            return Encoding.UTF8.GetString(data);
        }

        /// <summary>
        /// Decodes a byte array into a <see cref="String"/> key.
        /// </summary>
        /// <param name="data">The bte array to decode into a string key.</param>
        /// <param name="index">The index to start at within the byte array.</param>
        /// <param name="count">The number of bytes to decode.</param>
        /// <returns>A <see cref="String"/> representation of the byte array.</returns>
        public static string DecodeKey(byte[] data, int index, int count)
        {
            if (data == null || data.Length == 0 || count == 0) return null;

            return Encoding.UTF8.GetString(data, index, count);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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