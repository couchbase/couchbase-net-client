using System;
using System.Text;

namespace Couchbase.IO.Converters
{

    /// <summary>
    /// Provides methods for "manually" converting bytes to and from types.
    /// </summary>
    [Obsolete("Use DefaultConverter.")]
    public sealed class ManualByteConverter : IByteConverter
    {
        public bool ToBoolean(byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public float ToSingle(byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public DateTime ToDateTime(byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public double ToDouble(byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Converts a <see cref="byte"/> at a given offset to a <see cref="byte"/>.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        public byte ToByte(byte[] buffer, int offset)
        {
            return buffer[offset];
        }

        /// <summary>
        /// Converts a buffer at a given offset to a <see cref="short"/>.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        public short ToInt16(byte[] buffer, int offset)
        {
            return (short) ((buffer[offset++] << 8) |  buffer[offset++]);
        }

        /// <summary>
        /// Converts a buffer at a given offset to a <see cref="ushort"/>.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        public ushort ToUInt16(byte[] buffer, int offset)
        {
            return (ushort)((buffer[offset++] << 8) | buffer[offset++]);
        }

        /// <summary>
        /// Converts a buffer at a given offset to a <see cref="int"/>.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        public int ToInt32(byte[] buffer, int offset)
        {
            return (buffer[offset++] << 24) |
                   (buffer[offset++] << 16) |
                   (buffer[offset++] << 8)  |
                    buffer[offset++];
        }

        /// <summary>
        /// Converts a buffer at a given offset to a <see cref="uint"/>.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        public uint ToUInt32(byte[] buffer, int offset)
        {
            return (uint)((buffer[offset++] << 24) |
                          (buffer[offset++] << 16) |
                          (buffer[offset++] << 8)  |
                           buffer[offset++]);
        }

        /// <summary>
        /// Converts a buffer at a given offset to a <see cref="long"/>.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        public long ToInt64(byte[] buffer, int offset)
        {
            return (buffer[offset++] << 56) |
                   (buffer[offset++] << 48) |
                   (buffer[offset++] << 40) |
                   (buffer[offset++] << 32) |
                   (buffer[offset++] << 24) |
                   (buffer[offset++] << 16) |
                   (buffer[offset++] << 8)  |
                    buffer[offset++];
        }

        /// <summary>
        /// Converts a buffer at a given offset to a <see cref="ulong"/>.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        public ulong ToUInt64(byte[] buffer, int offset)
        {
            return (ulong) ((buffer[offset++] << 56) |
                            (buffer[offset++] << 48) |
                            (buffer[offset++] << 40) |
                            (buffer[offset++] << 32) |
                            (buffer[offset++] << 24) |
                            (buffer[offset++] << 16) |
                            (buffer[offset++] << 8)  |
                             buffer[offset++]);
        }

        /// <summary>
        /// Converts a buffer at a given offset to a <see cref="string"/>.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="length">The length.</param>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public string ToString(byte[] buffer, int offset, int length)
        {
            return Encoding.UTF8.GetString(buffer, offset, length);
        }

        /// <summary>
        /// Writes a <see cref="byte"/> to a buffer at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <remarks>Will create the buffer if null or empty.</remarks>
        public void FromByte(byte value, ref byte[] buffer, int offset)
        {
            if (buffer == null || buffer.Length == 0)
            {
                buffer = new byte[1];
            }
            FromByte(value, buffer, offset);
        }

        /// <summary>
        /// Writes a <see cref="byte"/> to a buffer at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromByte(byte value, byte[] buffer, int offset)
        {
            buffer[offset] = value;
        }

        /// <summary>
        /// Writes a <see cref="short"/> to a buffer at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <remarks>Will create the buffer if null or empty.</remarks>
        public void FromInt16(short value, ref byte[] buffer, int offset)
        {
            if (buffer == null || buffer.Length == 0)
            {
                buffer = new byte[2];
            }
            FromInt16(value, buffer, offset);
        }

        /// <summary>
        /// Writes a <see cref="ushort"/> to a buffer at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <remarks>Will create the buffer if null or empty.</remarks>
        public void FromUInt16(ushort value, ref byte[] buffer, int offset)
        {
            if (buffer == null || buffer.Length == 0)
            {
                buffer = new byte[2];
            }
            FromUInt16(value, buffer, offset);
        }

        /// <summary>
        /// Writes a <see cref="short"/> to a buffer at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromInt16(short value, byte[] buffer, int offset)
        {
            buffer[offset++] = (byte)(value >> 8);
            buffer[offset++] = (byte)(value & 255);
        }

        /// <summary>
        /// Writes a <see cref="ushort"/> to a buffer at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromUInt16(ushort value, byte[] buffer, int offset)
        {
            buffer[offset++] = (byte)(value >> 8);
            buffer[offset++] = (byte)(value & 255);
        }

        /// <summary>
        /// Writes a <see cref="int"/> to a buffer at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <remarks>Will create the buffer if null or empty.</remarks>
        public void FromInt32(int value, ref byte[] buffer, int offset)
        {
            if (buffer == null || buffer.Length == 0)
            {
                buffer = new byte[4];
            }
            FromInt32(value, buffer, offset);
        }

        /// <summary>
        /// Writes a <see cref="uint"/> to a buffer at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <remarks>Will create the buffer if null or empty.</remarks>
        public void FromUInt32(uint value, ref byte[] buffer, int offset)
        {
            if (buffer == null || buffer.Length == 0)
            {
                buffer = new byte[4];
            }
            FromUInt32(value, buffer, offset);
        }

        /// <summary>
        /// Writes a <see cref="int"/> to a buffer at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromInt32(int value, byte[] buffer, int offset)
        {
            buffer[offset++] = (byte)(value >> 24);
            buffer[offset++] = (byte)(value >> 16);
            buffer[offset++] = (byte)(value >> 8);
            buffer[offset++] = (byte)(value & 255);
        }

        /// <summary>
        /// Writes a <see cref="uint"/> to a buffer at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromUInt32(uint value, byte[] buffer, int offset)
        {
            buffer[offset++] = (byte)(value >> 24);
            buffer[offset++] = (byte)(value >> 16);
            buffer[offset++] = (byte)(value >> 8);
            buffer[offset++] = (byte)(value & 255);
        }

        /// <summary>
        /// Writes a <see cref="long"/> to a buffer at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <remarks>Will create the buffer if null or empty.</remarks>
        public void FromInt64(long value, ref byte[] buffer, int offset)
        {
            if (buffer == null || buffer.Length == 0)
            {
                buffer = new byte[8];
            }
            FromInt64(value, buffer, offset);
        }

        /// <summary>
        /// Writes a <see cref="ulong"/> to a buffer at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <remarks>Will create the buffer if null or empty.</remarks>
        public void FromUInt64(ulong value, ref byte[] buffer, int offset)
        {
            if (buffer == null || buffer.Length == 0)
            {
                buffer = new byte[8];
            }
            FromUInt64(value, buffer, offset);
        }

        /// <summary>
        /// Writes a <see cref="ulong"/> to a buffer at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromInt64(long value, byte[] buffer, int offset)
        {
            buffer[offset++] = (byte)(value >> 56);
            buffer[offset++] = (byte)(value >> 48);
            buffer[offset++] = (byte)(value >> 40);
            buffer[offset++] = (byte)(value >> 32);
            buffer[offset++] = (byte)(value >> 24);
            buffer[offset++] = (byte)(value >> 16);
            buffer[offset++] = (byte)(value >> 8);
            buffer[offset++] = (byte)(value & 255);
        }

        /// <summary>
        /// Writes a <see cref="ulong"/> to a buffer at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromUInt64(ulong value, byte[] buffer, int offset)
        {
            buffer[offset++] = (byte)(value >> 56);
            buffer[offset++] = (byte)(value >> 48);
            buffer[offset++] = (byte)(value >> 40);
            buffer[offset++] = (byte)(value >> 32);
            buffer[offset++] = (byte)(value >> 24);
            buffer[offset++] = (byte)(value >> 16);
            buffer[offset++] = (byte)(value >> 8);
            buffer[offset++] = (byte)(value & 255);
        }

        /// <summary>
        /// Writes a <see cref="string"/> to a buffer at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <remarks>Will resize buffer if empty.</remarks>
        public void FromString(string value, ref byte[] buffer, int offset)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            if (buffer.Length == 0)
            {
                buffer = new byte[bytes.Length];
            }
            Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
        }

        /// <summary>
        /// Writes a <see cref="string"/> to a buffer at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromString(string value, byte[] buffer, int offset)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
        }

        public void FromUInt642(ulong value, byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public void SetBit(ref byte theByte, int position, bool value)
        {
            if (value)
            {
                theByte = (byte)(theByte | (1 << position));
            }
            else
            {
                theByte = (byte)(theByte & ~(1 << position));
            }
        }

        public bool GetBit(byte theByte, int position)
        {
            return ((theByte & (1 << position)) != 0);
        }


        public short ToInt16(byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public ushort ToUInt16(byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public int ToInt32(byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public uint ToUInt32(byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public long ToInt64(byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public ulong ToUInt64(byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public void FromInt16(short value, ref byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public void FromUInt16(ushort value, ref byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public void FromInt32(int value, ref byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public void FromUInt32(uint value, ref byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public void FromInt64(long value, ref byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }


        public void FromUInt64(ulong value, ref byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
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
