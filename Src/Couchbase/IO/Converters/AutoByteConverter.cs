using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.IO.Converters
{
    public sealed class AutoByteConverter : IByteConverter
    {
        public byte ToByte(byte[] buffer, int offset)
        {
            return buffer[offset];
        }

        public short ToInt16(byte[] buffer, int offset)
        {
            var array = new byte[2];
            Buffer.BlockCopy(buffer, offset, array, 0, 2);
            Array.Reverse(array);
            
            return BitConverter.ToInt16(array, 0);
        }

        public ushort ToUInt16(byte[] buffer, int offset)
        {
            var array = new byte[2];
            Buffer.BlockCopy(buffer, offset, array, 0, 2);
            Array.Reverse(array);

            return BitConverter.ToUInt16(array, 0);
        }

        public int ToInt32(byte[] buffer, int offset)
        {
            var array = new byte[4];
            Buffer.BlockCopy(buffer, offset, array, 0, 4);
            Array.Reverse(array);

            return BitConverter.ToInt32(array, 0);
        }

        public uint ToUInt32(byte[] buffer, int offset)
        {
            var array = new byte[4];
            Buffer.BlockCopy(buffer, offset, array, 0, 4);
            Array.Reverse(array);

            return BitConverter.ToUInt32(array, 0);
        }

        public long ToInt64(byte[] buffer, int offset)
        {
            var array = new byte[8];
            Buffer.BlockCopy(buffer, offset, array, 0, 8);
            Array.Reverse(array);

            return BitConverter.ToInt64(array, 0);
        }

        public ulong ToUInt64(byte[] buffer, int offset)
        {
            var array = new byte[8];
            Buffer.BlockCopy(buffer, offset, array, 0, 8);
            Array.Reverse(array);

            return BitConverter.ToUInt64(array, 0);
        }

        public string ToString(byte[] buffer, int offset, int length)
        {
            var array = new byte[length];
            Buffer.BlockCopy(buffer, offset, array, 0, length);
 
            return Encoding.UTF8.GetString(array);
        }

        public void FromInt16(short value, ref byte[] buffer, int offset)
        {
            if (buffer.Length == 0)
            {
                buffer = new byte[2];
            }
            var array = BitConverter.GetBytes(value);
            Array.Reverse(array);
            Buffer.BlockCopy(array, 0, buffer, offset, 2);
        }

        public void FromInt16(short value, byte[] buffer, int offset)
        {
            FromInt16(value, ref buffer, offset);
        }

        public void FromUInt16(ushort value, ref byte[] buffer, int offset)
        {
            if (buffer.Length == 0)
            {
                buffer = new byte[2];
            }
            var array = BitConverter.GetBytes(value);
            Array.Reverse(array);
            Buffer.BlockCopy(array, 0, buffer, offset, 2);
        }

        public void FromUInt16(ushort value, byte[] buffer, int offset)
        {
            FromUInt16(value, ref buffer, offset);
        }

        public void FromInt32(int value, ref byte[] buffer, int offset)
        {
            if (buffer.Length == 0)
            {
                buffer = new byte[4];
            }
            var array = BitConverter.GetBytes(value);
            Array.Reverse(array);
            Buffer.BlockCopy(array, 0, buffer, offset, 4);
        }

        public void FromUInt32(uint value, ref byte[] buffer, int offset)
        {
            if (buffer.Length == 0)
            {
                buffer = new byte[4];
            }
            var array = BitConverter.GetBytes(value);
            Array.Reverse(array);
            Buffer.BlockCopy(array, 0, buffer, offset, 4);
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
            if (buffer.Length == 0)
            {
                buffer = new byte[8];
            }
            var array = BitConverter.GetBytes(value);
            Array.Reverse(array);
            Buffer.BlockCopy(array, 0, buffer, offset, 8);
        }

        public void FromUInt64(ulong value, ref byte[] buffer, int offset)
        {
            if (buffer.Length == 0)
            {
                buffer = new byte[8];
            }
            var array = BitConverter.GetBytes(value);
            Array.Reverse(array);
            Buffer.BlockCopy(array, 0, buffer, offset, 8);
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
