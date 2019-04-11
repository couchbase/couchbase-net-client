using System;
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
        #region Private helpers

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

        #endregion

        #region ToXXX

        /// <inheritdoc />
        public bool ToBoolean(ReadOnlySpan<byte> buffer, bool useNbo)
        {
            return Read<bool>(buffer, useNbo);
        }

        /// <inheritdoc />
        public float ToSingle(ReadOnlySpan<byte> buffer, bool useNbo)
        {
            return Read<float>(buffer, useNbo);
        }

        /// <inheritdoc />
        public DateTime ToDateTime(ReadOnlySpan<byte> buffer, bool useNbo)
        {
            return DateTime.FromBinary(Read<long>(buffer, useNbo));
        }

        /// <inheritdoc />
        public double ToDouble(ReadOnlySpan<byte> buffer, bool useNbo)
        {
            return Read<double>(buffer, useNbo);
        }

        /// <inheritdoc />
        public byte ToByte(ReadOnlySpan<byte> buffer)
        {
            return buffer[0];
        }

        /// <inheritdoc />
        public short ToInt16(ReadOnlySpan<byte> buffer, bool useNbo)
        {
            return Read<short>(buffer, useNbo);
        }

        /// <inheritdoc />
        public ushort ToUInt16(ReadOnlySpan<byte> buffer, bool useNbo)
        {
            return Read<ushort>(buffer, useNbo);
        }

        /// <inheritdoc />
        public int ToInt32(ReadOnlySpan<byte> buffer, bool useNbo)
        {
            return Read<int>(buffer, useNbo);
        }

        /// <inheritdoc />
        public uint ToUInt32(ReadOnlySpan<byte> buffer, bool useNbo)
        {
            return Read<uint>(buffer, useNbo);
        }

        /// <inheritdoc />
        public long ToInt64(ReadOnlySpan<byte> buffer, bool useNbo)
        {
            return Read<long>(buffer, useNbo);
        }

        /// <inheritdoc />
        public ulong ToUInt64(ReadOnlySpan<byte> buffer, bool useNbo)
        {
            return Read<ulong>(buffer, useNbo);
        }

        /// <inheritdoc />
        public unsafe string ToString(ReadOnlySpan<byte> buffer)
        {
            fixed (byte* bytes = &MemoryMarshal.GetReference(buffer))
            {
                return Encoding.UTF8.GetString(bytes, buffer.Length);
            }
        }

        #endregion

        #region FromXXX

        /// <inheritdoc />
        public void FromByte(byte value, Span<byte> buffer)
        {
            buffer[0] = value;
        }

        /// <inheritdoc />
        public void FromInt16(short value, Span<byte> buffer, bool useNbo)
        {
            Write(value, buffer, useNbo);
        }

        /// <inheritdoc />
        public void FromUInt16(ushort value, Span<byte> buffer, bool useNbo)
        {
            Write(value, buffer, useNbo);
        }

        /// <inheritdoc />
        public void FromInt32(int value, Span<byte> buffer, bool useNbo)
        {
            Write(value, buffer, useNbo);
        }

        /// <inheritdoc />
        public void FromUInt32(uint value, Span<byte> buffer, bool useNbo)
        {
            Write(value, buffer, useNbo);
        }

        /// <inheritdoc />
        public void FromInt64(long value, Span<byte> buffer, bool useNbo)
        {
            Write(value, buffer, useNbo);
        }

       /// <inheritdoc />
        public void FromUInt64(ulong value, Span<byte> buffer, bool useNbo)
        {
            Write(value, buffer, useNbo);
        }

        /// <inheritdoc />
        public int GetStringByteCount(string value)
        {
            return Encoding.UTF8.GetByteCount(value);
        }

        /// <inheritdoc />
        public unsafe int FromString(string value, Span<byte> buffer)
        {
            fixed (char* chars = value)
            {
                fixed (byte* bytes = &MemoryMarshal.GetReference(buffer))
                {
                    return Encoding.UTF8.GetBytes(chars, value.Length, bytes, buffer.Length);
                }
            }
        }

        #endregion

        #region Bits

        /// <inheritdoc />
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

        /// <inheritdoc />
        public bool GetBit(byte theByte, int position)
        {
            return ((theByte & (1 << position)) != 0);
        }

        #endregion
    }
}
