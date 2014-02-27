using System;
using System.Text;

namespace Couchbase.IO.Utils
{
    internal static class BinaryConverter
    {
        public static ushort DecodeUInt16(byte[] buffer, int offset)
        {
            return (ushort)((buffer[offset] << 8) + buffer[offset + 1]);
        }

        public static unsafe ushort DecodeUInt16(byte* buffer, int offset)
        {
            return (ushort)((buffer[offset] << 8) + buffer[offset + 1]);
        }

        public static unsafe int DecodeInt32(ArraySegment<byte> segment, int offset)
        {
            fixed (byte* buffer = segment.Array)
            {
                return DecodeInt32(buffer, 0);
            }
        }

        public static unsafe int DecodeInt32(byte* buffer, int offset)
        {
            buffer += offset;

            return (buffer[0] << 24) | (buffer[1] << 16) | (buffer[2] << 8) | buffer[3];
        }

        public static int DecodeInt32(byte[] buffer, int offset)
        {
            return (buffer[offset] << 24) | (buffer[offset + 1] << 16) | (buffer[offset + 2] << 8) | buffer[offset + 3];
        }

        public static uint DecodeUInt32(byte[] buffer, int offset)
        {
            return (uint)DecodeInt32(buffer, offset);
        }

        public static unsafe ulong DecodeUInt64(byte[] buffer, int offset)
        {
            fixed (byte* ptr = buffer)
            {
                return DecodeUInt64(ptr, offset);
            }
        }

        public static unsafe ulong DecodeUInt64(byte* buffer, int offset)
        {
            buffer += offset;
            var part1 = (uint)((buffer[0] << 24) | (buffer[1] << 16) | (buffer[2] << 8) | buffer[3]);
            var part2 = (uint)((buffer[4] << 24) | (buffer[5] << 16) | (buffer[6] << 8) | buffer[7]);
            return ((ulong)part1 << 32) | part2;
        }

        public static unsafe void EncodeUInt16(uint value, byte[] buffer, int offset)
        {
            fixed (byte* bufferPtr = buffer)
            {
                EncodeUInt16(value, bufferPtr, offset);
            }
        }

        public static unsafe void EncodeUInt16(uint value, byte* buffer, int offset)
        {
            byte* ptr = buffer + offset;
            ptr[0] = (byte)(value >> 8);
            ptr[1] = (byte)(value & 255);
        }

        public static unsafe void EncodeUInt32(uint value, byte[] buffer, int offset)
        {
            fixed (byte* bufferPtr = buffer)
            {
                EncodeUInt32(value, bufferPtr, offset);
            }
        }

        public static unsafe void EncodeUInt32(uint value, byte* buffer, int offset)
        {
            byte* ptr = buffer + offset;
            ptr[0] = (byte)(value >> 24);
            ptr[1] = (byte)(value >> 16);
            ptr[2] = (byte)(value >> 8);
            ptr[3] = (byte)(value & 255);
        }

        public static unsafe void EncodeUInt64(ulong value, byte[] buffer, int offset)
        {
            fixed (byte* bufferPtr = buffer)
            {
                EncodeUInt64(value, bufferPtr, offset);
            }
        }

        public static unsafe void EncodeUInt64(ulong value, byte* buffer, int offset)
        {
            byte* ptr = buffer + offset;
            ptr[0] = (byte)(value >> 56);
            ptr[1] = (byte)(value >> 48);
            ptr[2] = (byte)(value >> 40);
            ptr[3] = (byte)(value >> 32);
            ptr[4] = (byte)(value >> 24);
            ptr[5] = (byte)(value >> 16);
            ptr[6] = (byte)(value >> 8);
            ptr[7] = (byte)(value & 255);
        }

        public static byte[] EncodeKey(string key)
        {
            if (String.IsNullOrEmpty(key)) return null;

            return Encoding.UTF8.GetBytes(key);
        }

        public static string DecodeKey(byte[] data)
        {
            if (data == null || data.Length == 0) return null;

            return Encoding.UTF8.GetString(data);
        }

        public static string DecodeKey(byte[] data, int index, int count)
        {
            if (data == null || data.Length == 0 || count == 0) return null;

            return Encoding.UTF8.GetString(data, index, count);
        }
    }
}
