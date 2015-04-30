using System;
using System.Text;

namespace Couchbase.IO.Converters
{
    /// <summary>
    /// The default <see cref="IByteConverter" /> for for converting types and arrays before
    /// being sent or after being received across the network. Unless an overload is called
    /// with useNbo = false, Network Byte Order will be used in the conversion.
    /// </summary>
    public sealed class DefaultConverter : IByteConverter
    {
        static byte[] CopyAndReverse(byte[] src, int offset, int length)
        {
            var dst = new byte[length];
            for (var i = dst.Length; i > 0; i--)
            {
                dst[i - 1] = src[offset++];
            }
            return dst;
        }

        static void CopyAndReverse(byte[] src, ref byte[] dst, int offset, int length)
        {
            if (dst.Length == 0)
            {
                dst = new byte[length];
            }
            for (var i = src.Length; i > 0; i--)
            {
                dst[offset++] = src[i - 1];
            }
        }

        /// <summary>
        /// Reads a <see cref="Byte" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        public byte ToByte(byte[] buffer, int offset)
        {
            return buffer[offset];
        }

        /// <summary>
        /// Reads a <see cref="Int16" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        public short ToInt16(byte[] buffer, int offset)
        {
            return ToInt16(buffer, offset, true);
        }

        /// <summary>
        /// Reads a <see cref="Int16" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <returns></returns>
        public short ToInt16(byte[] buffer, int offset, bool useNbo)
        {
            if (useNbo)
            {
                const int length = 2;
                var array = CopyAndReverse(buffer, offset, length);
                return BitConverter.ToInt16(array, 0);
            }
            return BitConverter.ToInt16(buffer, offset);
        }

        /// <summary>
        /// Reads a <see cref="UInt16" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        public ushort ToUInt16(byte[] buffer, int offset)
        {
            return ToUInt16(buffer, offset, true);
        }

        /// <summary>
        /// Reads a <see cref="UInt16" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <returns></returns>
        public ushort ToUInt16(byte[] buffer, int offset, bool useNbo)
        {
            if (useNbo)
            {
                const int length = 2;
                var array = CopyAndReverse(buffer, offset, length);
                return BitConverter.ToUInt16(array, 0);
            }
            return BitConverter.ToUInt16(buffer, offset);
        }

        /// <summary>
        /// Reads a <see cref="Int32" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        public int ToInt32(byte[] buffer, int offset)
        {
            return ToInt32(buffer, offset, true);
        }

        /// <summary>
        /// Reads a <see cref="Int32" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <returns></returns>
        public int ToInt32(byte[] buffer, int offset, bool useNbo)
        {
            if (useNbo)
            {
                const int length = 4;
                var array = CopyAndReverse(buffer, offset, length);
                return BitConverter.ToInt32(array, 0);
            }
            return BitConverter.ToInt32(buffer, offset);
        }

        /// <summary>
        /// Reads a <see cref="UInt32" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        public uint ToUInt32(byte[] buffer, int offset)
        {
            return ToUInt32(buffer, offset, true);
        }

        /// <summary>
        /// Reads a <see cref="UInt32" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <returns></returns>
        public uint ToUInt32(byte[] buffer, int offset, bool useNbo)
        {
            if (useNbo)
            {
                const int length = 4;
                var array = CopyAndReverse(buffer, offset, length);
                return BitConverter.ToUInt32(array, 0);
            }
            return BitConverter.ToUInt32(buffer, offset);
        }

        /// <summary>
        /// Reads a <see cref="Int64" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        public long ToInt64(byte[] buffer, int offset)
        {
            const int length = 8;
            var array = CopyAndReverse(buffer, offset, length);
            return BitConverter.ToInt64(array, 0);
        }

        /// <summary>
        /// Reads a <see cref="Int64" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <returns></returns>
        public long ToInt64(byte[] buffer, int offset, bool useNbo)
        {
            if (useNbo)
            {
                const int length = 8;
                var array = CopyAndReverse(buffer, offset, length);
                return BitConverter.ToInt64(array, 0);
            }
            return BitConverter.ToInt64(buffer, offset);
        }

        /// <summary>
        /// Reads a <see cref="UInt64" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        public ulong ToUInt64(byte[] buffer, int offset)
        {
            return ToUInt64(buffer, offset, true);
        }

        /// <summary>
        /// Reads a <see cref="UInt64" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <returns></returns>
        public ulong ToUInt64(byte[] buffer, int offset, bool useNbo)
        {
            if (useNbo)
            {
                const int length = 8;
                var array = CopyAndReverse(buffer, offset, length);
                return BitConverter.ToUInt64(array, 0);
            }
            return BitConverter.ToUInt64(buffer, offset);
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
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
        /// Writes a <see cref="Int16" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <param name="offset">The offset.</param>
        public void FromInt16(short value, ref byte[] buffer, int offset, bool useNbo)
        {
            const int length = 2;
            var src = BitConverter.GetBytes(value);
            if (useNbo)
            {
                CopyAndReverse(src, ref buffer, offset, length);
            }
            else
            {
                buffer = new byte[length];
                Buffer.BlockCopy(src, 0, buffer, offset, length);
            }
        }

        /// <summary>
        /// Writes a <see cref="Int16" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromInt16(short value, ref byte[] buffer, int offset)
        {
            FromInt16(value, ref buffer, offset, true);
        }

        /// <summary>
        /// Writes a <see cref="Int16" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromInt16(short value, byte[] buffer, int offset)
        {
            FromInt16(value, ref buffer, offset, true);
        }

        /// <summary>
        /// Writes a <see cref="UInt16" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <param name="offset">The offset.</param>
        public void FromUInt16(ushort value, ref byte[] buffer, int offset, bool useNbo)
        {
            const int length = 2;
            var src = BitConverter.GetBytes(value);
            if (useNbo)
            {
                CopyAndReverse(src, ref buffer, offset, length);
            }
            else
            {
                buffer = new byte[length];
                Buffer.BlockCopy(src, 0, buffer, offset, length);
            }
        }

        /// <summary>
        /// Writes a <see cref="UInt16" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromUInt16(ushort value, ref byte[] buffer, int offset)
        {
            FromUInt16(value, ref buffer, offset, true);
        }

        /// <summary>
        /// Writes a <see cref="UInt16" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromUInt16(ushort value, byte[] buffer, int offset)
        {
            FromUInt16(value, ref buffer, offset);
        }

        /// <summary>
        /// Writes a <see cref="Int32" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        public void FromInt32(int value, ref byte[] buffer, int offset, bool useNbo)
        {
            const int length = 4;
            var src = BitConverter.GetBytes(value);
            if (useNbo)
            {
                CopyAndReverse(src, ref buffer, offset, length);
            }
            else
            {
                buffer = new byte[length];
                Buffer.BlockCopy(src, 0, buffer, offset, length);
            }
        }

        /// <summary>
        /// Writes a <see cref="Int32" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromInt32(int value, ref byte[] buffer, int offset)
        {
            FromInt32(value, ref buffer, offset, true);
        }

        /// <summary>
        /// Writes a <see cref="Int32" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromInt32(int value, byte[] buffer, int offset)
        {
            FromInt32(value, ref buffer, offset);
        }

        /// <summary>
        /// Writes a <see cref="UInt32" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromUInt32(uint value, byte[] buffer, int offset)
        {
            FromUInt32(value, ref buffer, offset);
        }

        /// <summary>
        /// Writes a <see cref="UInt32" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromUInt32(uint value, ref byte[] buffer, int offset)
        {
            FromUInt32(value, ref buffer, offset, true);
        }

        /// <summary>
        /// Writes a <see cref="UInt32" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        public void FromUInt32(uint value, ref byte[] buffer, int offset, bool useNbo)
        {
            const int length = 4;
            var src = BitConverter.GetBytes(value);
            if (useNbo)
            {
                CopyAndReverse(src, ref buffer, offset, length);
            }
            else
            {
                buffer = new byte[length];
                Buffer.BlockCopy(src, 0, buffer, offset, length);
            }
        }

        /// <summary>
        /// Writes a <see cref="Int64" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        public void FromInt64(long value, ref byte[] buffer, int offset, bool useNbo)
        {
            const int length = 8;
            var src = BitConverter.GetBytes(value);
            if (useNbo)
            {
                CopyAndReverse(src, ref buffer, offset, length);
            }
            else
            {
                buffer = new byte[length];
                Buffer.BlockCopy(src, 0, buffer, offset, length);
            }
        }

        /// <summary>
        /// Writes a <see cref="Int64" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromInt64(long value, ref byte[] buffer, int offset)
        {
            FromInt64(value, ref buffer, offset, true);
        }

        /// <summary>
        /// Writes a <see cref="Int64" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromInt64(long value, byte[] buffer, int offset)
        {
            FromInt64(value, ref buffer, offset);
        }

        /// <summary>
        /// Writes a <see cref="UInt64" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        public void FromUInt64(ulong value, ref byte[] buffer, int offset, bool useNbo)
        {
            const int length = 8;
            var src = BitConverter.GetBytes(value);
            if (useNbo)
            {
                CopyAndReverse(src, ref buffer, offset, length);
            }
            else
            {
                buffer = new byte[length];
                Buffer.BlockCopy(src, 0, buffer, offset, length);
            }
        }

        /// <summary>
        /// Writes a <see cref="UInt64" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromUInt64(ulong value, ref byte[] buffer, int offset)
        {
            FromUInt64(value, ref buffer, offset, true);
        }

        /// <summary>
        /// Writes a <see cref="UInt64" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromUInt64(ulong value, byte[] buffer, int offset)
        {
            FromUInt64(value, ref buffer, offset);
        }

        /// <summary>
        /// Writes a <see cref="string"/> to a dst at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The dst.</param>
        /// <param name="offset">The offset.</param>
        /// <remarks>Will resize dst if empty.</remarks>
        public void FromString(string value, ref byte[] buffer, int offset)
        {
            if (buffer.Length == 0)
            {
                buffer = new byte[Encoding.UTF8.GetByteCount(value)];
            }
            Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, offset);
        }

        /// <summary>
        /// Writes a <see cref="string"/> to a dst at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The dst.</param>
        /// <param name="offset">The offset.</param>
        public void FromString(string value, byte[] buffer, int offset)
        {
            FromString(value, ref buffer, offset);
        }

        /// <summary>
        /// Writes a <see cref="byte" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromByte(byte value, ref byte[] buffer, int offset)
        {
            buffer[offset] = value;
        }

        /// <summary>
        /// Writes a <see cref="byte" /> to a buffer starting at a given offset.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        public void FromByte(byte value, byte[] buffer, int offset)
        {
            FromByte(value, ref buffer, offset);
        }

        /// <summary>
        /// Sets the bit from a <see cref="byte" /> at a given position.
        /// </summary>
        /// <param name="theByte">The byte.</param>
        /// <param name="position">The position.</param>
        /// <param name="value">if set to <c>true</c> [value].</param>
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

        /// <summary>
        /// Gets the bit as a <see cref="bool" /> from a <see cref="byte" /> at a given position.
        /// </summary>
        /// <param name="theByte">The byte.</param>
        /// <param name="position">The position.</param>
        /// <returns>
        /// True if the bit is set; otherwise false.
        /// </returns>
        public bool GetBit(byte theByte, int position)
        {
            return ((theByte & (1 << position)) != 0);
        }
    }
}
