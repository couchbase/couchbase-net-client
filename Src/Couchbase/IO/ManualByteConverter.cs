using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.IO
{

    /// <summary>
    /// Provides methods for "manually" converting bytes to and from types. 
    /// </summary>
    public sealed class ManualByteConverter : IByteConverter
    {
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
                   (buffer[offset++] << 8) |
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
                   (buffer[offset++] << 8) |
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
                   (buffer[offset++] << 8) |
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
                            (buffer[offset++] << 8) |
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
        public void FromString(string value, byte[] buffer, int offset)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            Buffer.BlockCopy(bytes, 0, buffer, offset, bytes.Length);
        }
    }
}
