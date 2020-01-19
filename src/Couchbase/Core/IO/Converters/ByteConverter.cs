using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Couchbase.Core.IO.Converters
{
    /// <summary>
    /// Converts types and arrays before being sent or after being received across the network.
    /// Unless an overload is called  with useNbo = false, Network Byte Order will be used in the conversion.
    /// </summary>
    public static partial class ByteConverter
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

        #endregion

        #region ToXXX

        /// <summary>
        /// Reads a <see cref="bool"/> from a buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="useNbo">if set to <c>true</c> [use nbo].</param>
        /// <returns></returns>
        public static bool ToBoolean(ReadOnlySpan<byte> buffer, bool useNbo)
        {
            return Read<bool>(buffer, useNbo);
        }

        /// <summary>
        /// Reads a <see cref="float"/> from a buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="useNbo">if set to <c>true</c> [use nbo].</param>
        /// <returns></returns>
        public static float ToSingle(ReadOnlySpan<byte> buffer, bool useNbo)
        {
            return Read<float>(buffer, useNbo);
        }

        /// <summary>
        /// Reads a <see cref="DateTime"/> from a buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="useNbo">if set to <c>true</c> [use nbo].</param>
        /// <returns></returns>
        public static DateTime ToDateTime(ReadOnlySpan<byte> buffer, bool useNbo)
        {
            return DateTime.FromBinary(ToInt64(buffer, useNbo));
        }

        /// <summary>
        /// To the double.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="useNbo">if set to <c>true</c> [use nbo].</param>
        /// <returns></returns>
        public static double ToDouble(ReadOnlySpan<byte> buffer, bool useNbo)
        {
            return Read<double>(buffer, useNbo);
        }

        /// <summary>
        /// Reads a <see cref="Int16" /> from a buffer starting.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <returns></returns>
        public static short ToInt16(ReadOnlySpan<byte> buffer, bool useNbo)
        {
            return useNbo
                ? BinaryPrimitives.ReadInt16BigEndian(buffer)
                : BinaryPrimitives.ReadInt16LittleEndian(buffer);
        }

        /// <summary>
        /// Reads a <see cref="UInt16" /> from a buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <returns></returns>
        public static ushort ToUInt16(ReadOnlySpan<byte> buffer, bool useNbo)
        {
            return useNbo
                ? BinaryPrimitives.ReadUInt16BigEndian(buffer)
                : BinaryPrimitives.ReadUInt16LittleEndian(buffer);
        }

        /// <summary>
        /// Reads a <see cref="Int32" /> from a buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <returns></returns>
        public static int ToInt32(ReadOnlySpan<byte> buffer, bool useNbo)
        {
            return useNbo
                ? BinaryPrimitives.ReadInt32BigEndian(buffer)
                : BinaryPrimitives.ReadInt32LittleEndian(buffer);
        }

        /// <summary>
        /// Reads a <see cref="UInt32" /> from a buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <returns></returns>
        public static uint ToUInt32(ReadOnlySpan<byte> buffer, bool useNbo)
        {
            return useNbo
                ? BinaryPrimitives.ReadUInt32BigEndian(buffer)
                : BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        }

        /// <summary>
        /// Reads a <see cref="Int64" /> from a buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <returns></returns>
        public static long ToInt64(ReadOnlySpan<byte> buffer, bool useNbo)
        {
            return useNbo
                ? BinaryPrimitives.ReadInt64BigEndian(buffer)
                : BinaryPrimitives.ReadInt64LittleEndian(buffer);
        }

        /// <summary>
        /// Reads a <see cref="UInt64" /> from a buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        /// <returns></returns>
        public static ulong ToUInt64(ReadOnlySpan<byte> buffer, bool useNbo)
        {
            return useNbo
                ? BinaryPrimitives.ReadUInt64BigEndian(buffer)
                : BinaryPrimitives.ReadUInt64LittleEndian(buffer);
        }

        /// <summary>
        /// Returns a <see cref="System.String" /> from the buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public static unsafe string ToString(ReadOnlySpan<byte> buffer)
        {
            fixed (byte* bytes = &MemoryMarshal.GetReference(buffer))
            {
                return Encoding.UTF8.GetString(bytes, buffer.Length);
            }
        }

        #endregion

        #region FromXXX

        /// <summary>
        /// Writes a <see cref="Int16" /> to a buffer.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        public static void FromInt16(short value, Span<byte> buffer, bool useNbo)
        {
            if (useNbo)
            {
                BinaryPrimitives.WriteInt16BigEndian(buffer, value);
            }
            else
            {
                BinaryPrimitives.WriteInt16LittleEndian(buffer, value);
            }
        }

        /// <summary>
        /// Writes a <see cref="UInt16" /> to a buffer.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        public static void FromUInt16(ushort value, Span<byte> buffer, bool useNbo)
        {
            if (useNbo)
            {
                BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
            }
            else
            {
                BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
            }
        }

        /// <summary>
        /// Writes a <see cref="Int32" /> to a buffer.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        public static void FromInt32(int value, Span<byte> buffer, bool useNbo)
        {
            if (useNbo)
            {
                BinaryPrimitives.WriteInt32BigEndian(buffer, value);
            }
            else
            {
                BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
            }
        }

        /// <summary>
        /// Writes a <see cref="UInt32" /> to a buffer.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        public static void FromUInt32(uint value, Span<byte> buffer, bool useNbo)
        {
            if (useNbo)
            {
                BinaryPrimitives.WriteUInt32BigEndian(buffer, value);
            }
            else
            {
                BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
            }
        }

        /// <summary>
        /// Writes a <see cref="Int64" /> to a buffer.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        public static void FromInt64(long value, Span<byte> buffer, bool useNbo)
        {
            if (useNbo)
            {
                BinaryPrimitives.WriteInt64BigEndian(buffer, value);
            }
            else
            {
                BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
            }
        }

        /// <summary>
        /// Writes a <see cref="UInt64" /> to a buffer.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <param name="useNbo">If <c>true</c> will make most significant byte first.</param>
        public static void FromUInt64(ulong value, Span<byte> buffer, bool useNbo)
        {
            if (useNbo)
            {
                BinaryPrimitives.WriteUInt64BigEndian(buffer, value);
            }
            else
            {
                BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
            }
        }

        /// <summary>
        /// Gets the number of bytes required to convert a string.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>The number of bytes required to convert the string.</returns>
        public static int GetStringByteCount(string value)
        {
            return Encoding.UTF8.GetByteCount(value);
        }

        /// <summary>
        /// Writes a <see cref="System.String"/> to a buffer.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        /// <returns>Number of bytes written to the buffer.</returns>
        public static unsafe int FromString(string value, Span<byte> buffer)
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
    }
}
