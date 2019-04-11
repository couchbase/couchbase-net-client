using System;

namespace Couchbase.Core.IO.Converters
{
    public static class ByteConverterExtensions
    {
        #region ToXXX

        /// <summary>
        ///  Reads a <see cref="Int16"/> from a buffer, using network byte order.
        /// </summary>
        /// <param name="converter">The converter.</param>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        public static short ToInt16(this IByteConverter converter, ReadOnlySpan<byte> buffer)
        {
            return converter.ToInt16(buffer, true);
        }

        /// <summary>
        /// Reads a <see cref="UInt16"/> from a buffer, using network byte order.
        /// </summary>
        /// <param name="converter">The converter.</param>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        public static ushort ToUInt16(this IByteConverter converter, ReadOnlySpan<byte> buffer)
        {
            return converter.ToUInt16(buffer, true);
        }

        /// <summary>
        /// Reads a <see cref="Int32" /> from a buffer, using network byte order.
        /// </summary>
        /// <param name="converter">The converter.</param>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        public static int ToInt32(this IByteConverter converter, ReadOnlySpan<byte> buffer)
        {
            return converter.ToInt32(buffer, true);
        }

        /// <summary>
        /// Reads a <see cref="UInt32"/> from a buffer, using network byte order.
        /// </summary>
        /// <param name="converter">The converter.</param>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        public static uint ToUInt32(this IByteConverter converter, ReadOnlySpan<byte> buffer)
        {
            return converter.ToUInt32(buffer, true);
        }

        /// <summary>
        /// Reads a <see cref="Int64"/> from a buffer, using network byte order.
        /// </summary>
        /// <param name="converter">The converter.</param>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        public static long ToInt64(this IByteConverter converter, ReadOnlySpan<byte> buffer)
        {
            return converter.ToInt64(buffer, true);
        }

        /// <summary>
        /// Reads a <see cref="UInt64"/> from a buffer, using network byte order.
        /// </summary>
        /// <param name="converter">The converter.</param>
        /// <param name="buffer">The buffer.</param>
        /// <returns></returns>
        public static ulong ToUInt64(this IByteConverter converter, ReadOnlySpan<byte> buffer)
        {
            return converter.ToUInt64(buffer, true);
        }

        #endregion

        #region FromXXX

        /// <summary>
        /// Writes a <see cref="Int16"/> to a buffer, using network byte order.
        /// </summary>
        /// <param name="converter">The converter.</param>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        public static void FromInt16(this IByteConverter converter, short value, Span<byte> buffer)
        {
            converter.FromInt16(value, buffer, true);
        }

        /// <summary>
        /// Writes a <see cref="UInt16"/> to a buffer, using network byte order.
        /// </summary>
        /// <param name="converter">The converter.</param>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        public static void FromUInt16(this IByteConverter converter, ushort value, Span<byte> buffer)
        {
            converter.FromUInt16(value, buffer, true);
        }

        /// <summary>
        /// Writes a <see cref="Int32"/> to a buffer, using network byte order.
        /// </summary>
        /// <param name="converter">The converter.</param>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        public static void FromInt32(this IByteConverter converter, int value, Span<byte> buffer)
        {
            converter.FromInt32(value, buffer, true);
        }

        /// <summary>
        /// Writes a <see cref="UInt32"/> to a buffer, using network byte order.
        /// </summary>
        /// <param name="converter">The converter.</param>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        public static void FromUInt32(this IByteConverter converter, uint value, Span<byte> buffer)
        {
            converter.FromUInt32(value, buffer, true);
        }

        /// <summary>
        /// Writes a <see cref="Int64"/> to a buffer, using network byte order.
        /// </summary>
        /// <param name="converter">The converter.</param>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        public static void FromInt64(this IByteConverter converter, long value, Span<byte> buffer)
        {
            converter.FromInt64(value, buffer, true);
        }

        /// <summary>
        /// Writes a <see cref="UInt64"/> to a buffer, using network byte order.
        /// </summary>
        /// <param name="converter">The converter.</param>
        /// <param name="value">The value.</param>
        /// <param name="buffer">The buffer.</param>
        public static void FromUInt64(this IByteConverter converter, ulong value, Span<byte> buffer)
        {
            converter.FromUInt64(value, buffer, true);
        }

        #endregion
    }
}
