using System;

namespace Couchbase.Core.IO.Converters
{
    public static partial class ByteConverter
    {
        #region ToXXX

        /// <summary>
        ///  Reads a <see cref="Int16"/> from a buffer, using network byte order.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        public static short ToInt16(ReadOnlySpan<byte> buffer)
        {
            return ToInt16(buffer, true);
        }

        /// <summary>
        /// Reads a <see cref="UInt16"/> from a buffer, using network byte order.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        public static ushort ToUInt16(ReadOnlySpan<byte> buffer)
        {
            return ToUInt16(buffer, true);
        }

        /// <summary>
        /// Reads a <see cref="Int32" /> from a buffer, using network byte order.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        public static int ToInt32(ReadOnlySpan<byte> buffer)
        {
            return ToInt32(buffer, true);
        }

        /// <summary>
        /// Reads a <see cref="UInt32"/> from a buffer, using network byte order.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        public static uint ToUInt32(ReadOnlySpan<byte> buffer)
        {
            return ToUInt32(buffer, true);
        }

        /// <summary>
        /// Reads a <see cref="Int64"/> from a buffer, using network byte order.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        public static long ToInt64(ReadOnlySpan<byte> buffer)
        {
            return ToInt64(buffer, true);
        }

        /// <summary>
        /// Reads a <see cref="UInt64"/> from a buffer, using network byte order.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        public static ulong ToUInt64(ReadOnlySpan<byte> buffer)
        {
            return ToUInt64(buffer, true);
        }

        #endregion

        #region FromXXX

        /// <summary>
        /// Writes a <see cref="Int16"/> to a buffer, using network byte order.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        public static void FromInt16(short value, Span<byte> buffer)
        {
            FromInt16(value, buffer, true);
        }

        /// <summary>
        /// Writes a <see cref="UInt16"/> to a buffer, using network byte order.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        public static void FromUInt16(ushort value, Span<byte> buffer)
        {
            FromUInt16(value, buffer, true);
        }

        /// <summary>
        /// Writes a <see cref="Int32"/> to a buffer, using network byte order.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        public static void FromInt32(int value, Span<byte> buffer)
        {
            FromInt32(value, buffer, true);
        }

        /// <summary>
        /// Writes a <see cref="UInt32"/> to a buffer, using network byte order.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        public static void FromUInt32(uint value, Span<byte> buffer)
        {
            FromUInt32(value, buffer, true);
        }

        /// <summary>
        /// Writes a <see cref="Int64"/> to a buffer, using network byte order.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        public static void FromInt64(long value, Span<byte> buffer)
        {
            FromInt64(value, buffer, true);
        }

        /// <summary>
        /// Writes a <see cref="UInt64"/> to a buffer, using network byte order.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        public static void FromUInt64(ulong value, Span<byte> buffer)
        {
            FromUInt64(value, buffer, true);
        }

        #endregion
    }
}
