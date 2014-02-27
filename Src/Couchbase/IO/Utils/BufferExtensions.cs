using System;
using Couchbase.IO.Operations;

namespace Couchbase.IO.Utils
{
    public static class BufferExtensions
    {
        public static TypeCode ToTypeCode(this byte[] buffer, int offset)
        {
            return (TypeCode)BinaryConverter.DecodeInt32(buffer, offset);
        }

        public static OperationCode ToOpCode(this byte value)
        {
            return (OperationCode)value;
        }

        public static ResponseStatus GetResponseStatus(this byte[] buffer, int offset)
        {
            return (ResponseStatus)BinaryConverter.DecodeUInt16(buffer, offset);
        }

        public static ulong GetUInt64(this byte[] buffer, int offset)
        {
            return BinaryConverter.DecodeUInt64(buffer, offset);
        }

        public static long GetInt64(this byte[] buffer, int offset)
        {
            return (long)GetUInt64(buffer, offset);
        }

        public static uint GetUInt32(this byte[] buffer, int offset)
        {
            return BinaryConverter.DecodeUInt32(buffer, offset);
        }

        public static int GetInt32(this byte[] buffer, int offset)
        {
            return BinaryConverter.DecodeInt32(buffer, offset);
        }

        public static ushort GetUInt16(this byte[] buffer, int offset)
        {
            return BinaryConverter.DecodeUInt16(buffer, offset);
        }

        public static short GetInt16(this byte[] buffer, int offset)
        {
            return (short)GetUInt16(buffer, offset);
        }
    }
}
