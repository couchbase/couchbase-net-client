using System;
using Couchbase.IO.Converters;

namespace Couchbase.Tests.Fakes
{
    public class FakeConverter : IByteConverter
    {
        public byte ToByte(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public short ToInt16(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public ushort ToUInt16(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public int ToInt32(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public uint ToUInt32(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public long ToInt64(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public ulong ToUInt64(byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public string ToString(byte[] buffer, int offset, int length)
        {
            throw new NotImplementedException();
        }

        public void FromInt16(short value, ref byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public void FromInt16(short value, byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public void FromUInt16(ushort value, ref byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public void FromUInt16(ushort value, byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public void FromInt32(int value, ref byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public void FromUInt32(uint value, ref byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public void FromInt32(int value, byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public void FromUInt32(uint value, byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public void FromInt64(long value, ref byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public void FromUInt64(ulong value, ref byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public void FromInt64(long value, byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public void FromUInt64(ulong value, byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public void FromString(string value, byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public void FromString(string value, ref byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public void FromByte(byte value, ref byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public void FromByte(byte value, byte[] buffer, int offset)
        {
            throw new NotImplementedException();
        }

        public void SetBit(ref byte theByte, int position, bool value)
        {
            throw new NotImplementedException();
        }

        public bool GetBit(byte theByte, int position)
        {
            throw new NotImplementedException();
        }


        public short ToInt16(byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public ushort ToUInt16(byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public int ToInt32(byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public uint ToUInt32(byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public long ToInt64(byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public ulong ToUInt64(byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public void FromInt16(short value, ref byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public void FromUInt16(ushort value, ref byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public void FromInt32(int value, ref byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public void FromUInt32(uint value, ref byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }

        public void FromInt64(long value, ref byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }


        public void FromUInt64(ulong value, ref byte[] buffer, int offset, bool useNbo)
        {
            throw new NotImplementedException();
        }
    }
}
