using System;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Couchbase.Core.IO.Converters
{
   /// <summary>
    /// The default <see cref="IByteConverter" /> for for converting types and arrays before
    /// being sent or after being received across the network. Unless an overload is called
    /// with useNbo = false, Network Byte Order will be used in the conversion.
    /// </summary>
    public sealed class DefaultConverter : IByteConverter
    {
        private static T Read<T>(ReadOnlySpan<byte> src, bool useNbo)
            where T: struct
        {
            if (useNbo)
            {
                Span<byte> dst = stackalloc byte[Unsafe.SizeOf<T>()];

                var j = 0;
                for (var i = dst.Length - 1; i >= 0; i--)
                {
                    dst[i] = src[j++];
                }

                return MemoryMarshal.Read<T>(dst);
            }
            else
            {
                return MemoryMarshal.Read<T>(src);
            }
        }

        private static void Write<T>(T value, ref byte[] dst, int offset, bool useNbo)
            where T: struct
        {
            if (dst.Length == 0)
            {
                dst = new byte[Unsafe.SizeOf<T>()];
            }

            Write(value, dst.AsSpan(offset), useNbo);
        }

        private static void Write<T>(T value, Span<byte> dst, bool useNbo)
            where T: struct
        {
            var size = Unsafe.SizeOf<T>();

            if (size > 1 && (useNbo ^ !BitConverter.IsLittleEndian))
            {
                // size is > 1 means byte order is significant
                // switch if useNbo = true and we are little endian, or useNbo = false and we are big endian

                Span<byte> buffer = stackalloc byte[size];
                MemoryMarshal.Write(buffer, ref value);

                var j = 0;
                for (var i = buffer.Length - 1; i >= 0; i--)
                {
                    dst[j++] = buffer[i];
                }
            }
            else
            {
                MemoryMarshal.Write(dst, ref value);
            }
        }

        /// <summary>
        /// Reads a <see cref="bool" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">if set to <c>true</c> [use nbo].</param>
        /// <returns></returns>
        public bool ToBoolean(byte[] buffer, int offset, bool useNbo)
        {
            return Read<bool>(buffer.AsSpan(offset), useNbo);
        }

        /// <summary>
        /// Reads a <see cref="float" /> from a buffer starting from a given offset..
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">if set to <c>true</c> [use nbo].</param>
        /// <returns></returns>
        public float ToSingle(byte[] buffer, int offset, bool useNbo)
        {
            return Read<float>(buffer.AsSpan(offset), useNbo);
        }

        /// <summary>
        /// Reads a <see cref="DateTime" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">if set to <c>true</c> [use nbo].</param>
        /// <returns></returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public DateTime ToDateTime(byte[] buffer, int offset, bool useNbo)
        {
            return DateTime.FromBinary(Read<long>(buffer.AsSpan(offset), useNbo));
        }

        /// <summary>
        /// Reads a <see cref="Double" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="useNbo">if set to <c>true</c> [use nbo].</param>
        /// <returns></returns>
        public double ToDouble(byte[] buffer, int offset, bool useNbo)
        {
            return Read<double>(buffer.AsSpan(offset), useNbo);
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
            return Read<short>(buffer.AsSpan(offset), useNbo);
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
            return Read<ushort>(buffer.AsSpan(offset), useNbo);
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
            return Read<int>(buffer.AsSpan(offset), useNbo);
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
            return Read<uint>(buffer.AsSpan(offset), useNbo);
        }

        /// <summary>
        /// Reads a <see cref="Int64" /> from a buffer starting from a given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns></returns>
        public long ToInt64(byte[] buffer, int offset)
        {
            return ToInt64(buffer, offset, true);
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
            return Read<long>(buffer.AsSpan(offset), useNbo);
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
            return Read<ulong>(buffer.AsSpan(offset), useNbo);
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
            Write(value, ref buffer, offset, useNbo);
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
            FromInt16(value, ref buffer, offset);
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
            Write(value, ref buffer, offset, useNbo);
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
            Write(value, ref buffer, offset, useNbo);
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
            Write(value, ref buffer, offset, useNbo);
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
            Write(value, ref buffer, offset, useNbo);
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
            Write(value, ref buffer, offset, useNbo);
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
