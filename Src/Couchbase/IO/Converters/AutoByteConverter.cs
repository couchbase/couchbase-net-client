using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.IO.Converters
{
    public sealed class AutoByteConverter : IByteConverter
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

        public byte ToByte(byte[] buffer, int offset)
        {
            return buffer[offset];
        }

        public short ToInt16(byte[] buffer, int offset)
        {
            const int length = 2;
            var array = CopyAndReverse(buffer, offset, length);
            return BitConverter.ToInt16(array, 0);
        }

        public ushort ToUInt16(byte[] buffer, int offset)
        {
            const int length = 2;
            var array = CopyAndReverse(buffer, offset, length);
            return BitConverter.ToUInt16(array, 0);
        }

        public int ToInt32(byte[] buffer, int offset)
        {
            const int length = 4;
            var array = CopyAndReverse(buffer, offset, length);
            return BitConverter.ToInt32(array, 0);
        }

        public uint ToUInt32(byte[] buffer, int offset)
        {
            const int length = 4;
            var array = CopyAndReverse(buffer, offset, length);
            return BitConverter.ToUInt32(array, 0);
        }

        public long ToInt64(byte[] buffer, int offset)
        {
            const int length = 8;
            var array = CopyAndReverse(buffer, offset, length);
            return BitConverter.ToInt64(array, 0);
        }

        public ulong ToUInt64(byte[] buffer, int offset)
        {
            const int length = 8;
            var array = CopyAndReverse(buffer, offset, length);
            return BitConverter.ToUInt64(array, 0);
        }

        public string ToString(byte[] buffer, int offset, int length)
        {
            return Encoding.UTF8.GetString(buffer, offset, length);
        }

        public void FromInt16(short value, ref byte[] buffer, int offset)
        {
            const int length = 2;
            var src = BitConverter.GetBytes(value);
            CopyAndReverse(src, ref buffer, offset, length);
        }

        public void FromInt16(short value, byte[] buffer, int offset)
        {
            FromInt16(value, ref buffer, offset);
        }

        public void FromUInt16(ushort value, ref byte[] buffer, int offset)
        {
            const int length = 2;
            var src = BitConverter.GetBytes(value);
            CopyAndReverse(src, ref buffer, offset, length);
        }

        public void FromUInt16(ushort value, byte[] buffer, int offset)
        {
            FromUInt16(value, ref buffer, offset);
        }

        public void FromInt32(int value, ref byte[] buffer, int offset)
        {
            const int length = 4;
            var src = BitConverter.GetBytes(value);
            CopyAndReverse(src, ref buffer, offset, length);
        }

        public void FromUInt32(uint value, ref byte[] buffer, int offset)
        {
            const int length = 4;
            var src = BitConverter.GetBytes(value);
            CopyAndReverse(src, ref buffer, offset, length);
        }

        public void FromInt32(int value, byte[] buffer, int offset)
        {
            FromInt32(value, ref buffer, offset);
        }

        public void FromUInt32(uint value, byte[] buffer, int offset)
        {
            FromUInt32(value, ref buffer, offset);
        }

        public void FromInt64(long value, ref byte[] buffer, int offset)
        {
            const int length = 8;
            var src = BitConverter.GetBytes(value);
            CopyAndReverse(src, ref buffer, offset, length);
        }

        public void FromUInt64(ulong value, ref byte[] buffer, int offset)
        {
            const int length = 8;
            var src = BitConverter.GetBytes(value);
            CopyAndReverse(src, ref buffer, offset, length);
        }

        public void FromInt64(long value, byte[] buffer, int offset)
        {
            FromInt64(value, ref buffer, offset);
        }

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

        public void FromByte(byte value, ref byte[] buffer, int offset)
        {
            buffer[offset] = value;
        }

        public void FromByte(byte value, byte[] buffer, int offset)
        {
            FromByte(value, ref buffer, offset);
        }
    }
}
