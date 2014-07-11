using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.IO.Converters
{
    /// <summary>
    /// Provides and interface for converting types and arrays before being sent or after being received across the network.
    /// </summary>
    public interface IByteConverter
    {
        byte ToByte(byte[] buffer, int offset);

        short ToInt16(byte[] buffer, int offset);

        ushort ToUInt16(byte[] buffer, int offset);

        int ToInt32(byte[] buffer, int offset);

        uint ToUInt32(byte[] buffer, int offset);

        long ToInt64(byte[] buffer, int offset);

        ulong ToUInt64(byte[] buffer, int offset);

        string ToString(byte[] buffer, int offset, int length);

        void FromInt16(short value, ref byte[] buffer, int offset);

        void FromInt16(short value, byte[] buffer, int offset);

        void FromUInt16(ushort value, ref byte[] buffer, int offset);

        void FromUInt16(ushort value, byte[] buffer, int offset);

        void FromInt32(int value, ref byte[] buffer, int offset);

        void FromUInt32(uint value, ref byte[] buffer, int offset);

        void FromInt32(int value, byte[] buffer, int offset);

        void FromUInt32(uint value, byte[] buffer, int offset);

        void FromInt64(long value,  ref byte[] buffer, int offset);

        void FromUInt64(ulong value, ref byte[] buffer, int offset);

        void FromInt64(long value, byte[] buffer, int offset);

        void FromUInt64(ulong value, byte[] buffer, int offset);

        void FromString(string value, byte[] buffer, int offset);

        void FromString(string value, ref byte[] buffer, int offset);

        void FromByte(byte value, ref byte[] buffer, int offset);

        void FromByte(byte value, byte[] buffer, int offset);
    }
}
