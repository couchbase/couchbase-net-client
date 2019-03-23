using System;
using System.Linq;
using Couchbase.Core.IO.Converters;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Converters
{
    public class DefaultConverterTests
    {
        #region To value from bytes

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void Test_ToBoolean(bool value, bool useNbo)
        {
            var converter = new DefaultConverter();
            var bytes =  BitConverter.GetBytes(value);
            if (useNbo)
            {
                bytes = bytes.Reverse().ToArray();
            }

            var actual = converter.ToBoolean(bytes, 0, useNbo);

            Assert.Equal(value, actual);
        }

        [Theory]
        [InlineData(2.3d, false)]
        [InlineData(2.3d, true)]
        [InlineData(628459.5d, false)]
        [InlineData(628459.5d, true)]
        public void Test_ToDouble(double value, bool useNbo)
        {
            var converter = new DefaultConverter();

            var bytes = BitConverter.GetBytes(value);
            if (useNbo)
            {
                bytes = bytes.Reverse().ToArray();
            }

            var actual = converter.ToDouble(bytes, 0, useNbo);

            Assert.Equal(value, actual);
        }

        [Theory]
        [InlineData(2.3f, false)]
        [InlineData(2.3f, true)]
        [InlineData(628459.5f, false)]
        [InlineData(628459.5f, true)]
        public void Test_ToSingle(float value, bool useNbo)
        {
            var converter = new DefaultConverter();

            var bytes = BitConverter.GetBytes(value);
            if (useNbo)
            {
                bytes = bytes.Reverse().ToArray();
            }

            var actual = converter.ToSingle(bytes, 0, useNbo);

            Assert.Equal(value, actual);
        }

        [Theory]
        [InlineData(1972, 12, 7, false)]
        [InlineData(1972, 12, 7, true)]
        public void Test_ToDateTime(int year, int month, int day, bool useNbo)
        {
            var converter = new DefaultConverter();

            var value = new DateTime(year, month, day);

            var bytes = BitConverter.GetBytes(value.ToBinary());
            if (useNbo)
            {
                bytes = bytes.Reverse().ToArray();
            }

            var actual = converter.ToDateTime(bytes, 0, useNbo);

            Assert.Equal(value, actual);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(128)]
        [InlineData(255)]
        public void Test_ToByte(byte value)
        {
            var converter = new DefaultConverter();

            var bytes = new byte[] {0, 0, value};

            var actual = converter.ToByte(bytes, 2);

            Assert.Equal(value, actual);
        }

        [Theory]
        [InlineData(5, false)]
        [InlineData(5, true)]
        [InlineData(31548, false)]
        [InlineData(31548, true)]
        public void Test_ToInt16(short value, bool useNbo)
        {
            var converter = new DefaultConverter();

            var bytes = BitConverter.GetBytes(value);
            if (useNbo)
            {
                bytes = bytes.Reverse().ToArray();
            }

            var actual = converter.ToInt16(bytes, 0, useNbo);

            Assert.Equal(value, actual);
        }

        [Theory]
        [InlineData(5)]
        [InlineData(31548)]
        public void Test_ToInt16_NoNboSpecified(short value)
        {
            var converter = new DefaultConverter();

            var bytes = BitConverter.GetBytes(value);
            bytes = bytes.Reverse().ToArray();

            var actual = converter.ToInt16(bytes, 0);

            Assert.Equal(value, actual);
        }

        [Theory]
        [InlineData(5, false)]
        [InlineData(5, true)]
        [InlineData(62845, false)]
        [InlineData(62845, true)]
        public void Test_ToUInt16(ushort value, bool useNbo)
        {
            var converter = new DefaultConverter();

            var bytes = BitConverter.GetBytes(value);
            if (useNbo)
            {
                bytes = bytes.Reverse().ToArray();
            }

            var actual = converter.ToUInt16(bytes, 0, useNbo);

            Assert.Equal(value, actual);
        }

        [Theory]
        [InlineData(5)]
        [InlineData(62845)]
        public void Test_ToUInt16_NoNboSpecified(ushort value)
        {
            var converter = new DefaultConverter();

            var bytes = BitConverter.GetBytes(value);
            bytes = bytes.Reverse().ToArray();

            var actual = converter.ToUInt16(bytes, 0);

            Assert.Equal(value, actual);
        }

        [Theory]
        [InlineData(5, false)]
        [InlineData(5, true)]
        [InlineData(31548, false)]
        [InlineData(31548, true)]
        [InlineData(2125894512, false)]
        [InlineData(2125894512, true)]
        public void Test_ToInt32(int value, bool useNbo)
        {
            var converter = new DefaultConverter();

            var bytes = BitConverter.GetBytes(value);
            if (useNbo)
            {
                bytes = bytes.Reverse().ToArray();
            }

            var actual = converter.ToInt32(bytes, 0, useNbo);

            Assert.Equal(value, actual);
        }

        [Theory]
        [InlineData(5)]
        [InlineData(31548)]
        [InlineData(2125894512)]
        public void Test_ToInt32_NoNboSpecified(int value)
        {
            var converter = new DefaultConverter();

            var bytes = BitConverter.GetBytes(value);
            bytes = bytes.Reverse().ToArray();

            var actual = converter.ToInt32(bytes, 0);

            Assert.Equal(value, actual);
        }

        [Theory]
        [InlineData(5u, false)]
        [InlineData(5u, true)]
        [InlineData(62845u, false)]
        [InlineData(62845u, true)]
        [InlineData(4125894512u, false)]
        [InlineData(4125894512u, true)]
        public void Test_ToUInt32(uint value, bool useNbo)
        {
            var converter = new DefaultConverter();

            var bytes = BitConverter.GetBytes(value);
            if (useNbo)
            {
                bytes = bytes.Reverse().ToArray();
            }

            var actual = converter.ToUInt32(bytes, 0, useNbo);

            Assert.Equal(value, actual);
        }

        [Theory]
        [InlineData(5u)]
        [InlineData(62845u)]
        [InlineData(4125894512u)]
        public void Test_ToUInt32_NoNboSpecified(uint value)
        {
            var converter = new DefaultConverter();

            var bytes = BitConverter.GetBytes(value);
            bytes = bytes.Reverse().ToArray();

            var actual = converter.ToUInt32(bytes, 0);

            Assert.Equal(value, actual);
        }

        [Theory]
        [InlineData(new byte[] {0xff, 0xff, 0xff, 0xff, 0xe5, 0x5d, 0x9f, 0xdf}, 18446744073262702559, true)]
        [InlineData(new byte[] {0xdf, 0x9f, 0x5d, 0xe5, 0xff, 0xff, 0xff, 0xff}, 18446744073262702559, false)]
        [InlineData(new byte[] {0x00, 0x00, 0xa9, 0x53, 0x5f, 0x3d, 0xa7, 0x0f}, 186175545255695, true)]
        [InlineData(new byte[] {0x0f, 0xa7, 0x3d, 0x5f, 0x53, 0xa9, 0x00, 0x00}, 186175545255695, false)]
        public void Test_ToUInt64(byte[] bytes, ulong expected, bool useNbo)
        {
            var converter = new DefaultConverter();
            var actual = converter.ToUInt64(bytes, 0, useNbo);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(new byte[] {0xff, 0xff, 0xff, 0xff, 0xe5, 0x5d, 0x9f, 0xdf}, 18446744073262702559)]
        [InlineData(new byte[] {0x00, 0x00, 0xa9, 0x53, 0x5f, 0x3d, 0xa7, 0x0f}, 186175545255695)]
        public void Test_ToUInt64_NoNboSpecified(byte[] bytes, ulong expected)
        {
            var converter = new DefaultConverter();
            var actual = converter.ToUInt64(bytes, 0);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(new byte[] {0x7f, 0xff, 0xff, 0xff, 0x5f, 0x3d, 0xa7, 0x0f}, 9223372034157684495, true)]
        [InlineData(new byte[] {0x0f, 0xa7, 0x3d, 0x5f, 0xff, 0xff, 0xff, 0x7f}, 9223372034157684495, false)]
        [InlineData(new byte[] {0x00, 0x00, 0xa9, 0x53, 0x5f, 0x3d, 0xa7, 0x0f}, 186175545255695, true)]
        [InlineData(new byte[] {0x0f, 0xa7, 0x3d, 0x5f, 0x53, 0xa9, 0x00, 0x00}, 186175545255695, false)]
        public void Test_ToInt64(byte[] bytes, long expected, bool useNbo)
        {
            var converter = new DefaultConverter();
            var actual = converter.ToInt64(bytes, 0, useNbo);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(new byte[] {0x7f, 0xff, 0xff, 0xff, 0x5f, 0x3d, 0xa7, 0x0f}, 9223372034157684495)]
        [InlineData(new byte[] {0x00, 0x00, 0xa9, 0x53, 0x5f, 0x3d, 0xa7, 0x0f}, 186175545255695)]
        public void Test_ToInt64_NoNboSpecified(byte[] bytes, long expected)
        {
            var converter = new DefaultConverter();
            var actual = converter.ToInt64(bytes, 0);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(new byte[] {0x0, 0x0, 0x48, 0x65, 0x6c, 0x6c, 0x6f, 0x92}, "Hello", 2, 5)]
        [InlineData(new byte[] {0x48, 0x65, 0x6c, 0x6c, 0x6f}, "Hello", 0, 5)]
        public void Test_ToString(byte[] bytes, string expected, int offset, int length)
        {
            var converter = new DefaultConverter();
            var actual = converter.ToString(bytes, offset, length);

            Assert.Equal(expected, actual);
        }

        #endregion

        #region from value to bytes

        [Theory]
        [InlineData(new byte[] {0x0, 0x0, 0x48, 0x65, 0x6c, 0x6c, 0x6f, 0x0}, "Hello", 2)]
        [InlineData(new byte[] {0x48, 0x65, 0x6c, 0x6c, 0x6f}, "Hello", 0)]
        public void Test_FromString(byte[] expected, string value, int offset)
        {
            var converter = new DefaultConverter();

            var buffer = new byte[32];
            converter.FromString(value, buffer, offset);

            // Buffer matches expected
            Assert.Equal(expected, buffer.Take(expected.Length));

            // Remainder of buffer is still zeroes
            Assert.All(buffer.Skip(expected.Length), p => Assert.Equal(0, p));
        }

        [Theory]
        [InlineData(new byte[] {0x48, 0x65, 0x6c, 0x6c, 0x6f}, "Hello")]
        public void Test_FromString_EmptyBuffer(byte[] expected, string value)
        {
            var converter = new DefaultConverter();

            var buffer = new byte[0];
            converter.FromString(value, ref buffer, 0);

            Assert.Equal(expected, buffer);
        }

        [Theory]
        [InlineData(5, 3, new byte[] {0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00})]
        [InlineData(15, 0, new byte[] {0x0f, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})]
        public void Test_FromByte(byte value, int offset, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[8];
            converter.FromByte(value, actual, offset);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5, 3, true, new byte[] {0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00})]
        [InlineData(5, 0, false, new byte[] {0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})]
        public void Test_FromInt16(short value, int offset, bool useNbo, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[8];
            converter.FromInt16(value, ref actual, offset, useNbo);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5, 3, new byte[] {0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00})]
        public void Test_FromInt16_NoNboSpecified(short value, int offset, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[8];
            converter.FromInt16(value, actual, offset);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5, 3, true, new byte[] {0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00})]
        [InlineData(5, 0, false, new byte[] {0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})]
        public void Test_FromUInt16(ushort value, int offset, bool useNbo, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[8];
            converter.FromUInt16(value, ref actual, offset, useNbo);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5, 3, new byte[] {0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00})]
        public void Test_FromUInt16_NoNboSpecified(ushort value, int offset, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[8];
            converter.FromUInt16(value, actual, offset);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5, 3, true, new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00})]
        [InlineData(5, 0, false, new byte[] {0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})]
        public void Test_FromInt32(int value, int offset, bool useNbo, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[11];
            converter.FromInt32(value, ref actual, offset, useNbo);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5, 3, new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00})]
        public void Test_FromInt32_NoNboSpecified(int value, int offset, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[11];
            converter.FromInt32(value, actual, offset);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5u, 3, true, new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00})]
        [InlineData(5u, 0, false, new byte[] {0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})]
        public void Test_FromUInt32(uint value, int offset, bool useNbo, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[11];
            converter.FromUInt32(value, ref actual, offset, useNbo);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5u, 3, new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00})]
        public void Test_FromUInt32_NoNboSpecified(uint value, int offset, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[11];
            converter.FromUInt32(value, actual, offset);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5, 3, true, new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00})]
        [InlineData(5, 0, false, new byte[] {0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})]
        public void Test_FromInt64(long value, int offset, bool useNbo, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[expected.Length];
            converter.FromInt64(value, ref actual, offset, useNbo);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5, 3, new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00})]
        public void Test_FromInt64_NoNboSpecified(long value, int offset, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[expected.Length];
            converter.FromInt64(value, actual, offset);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5, 3, true, new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00})]
        [InlineData(5, 0, false, new byte[] {0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})]
        public void Test_FromUInt64(ulong value, int offset, bool useNbo, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[expected.Length];
            converter.FromUInt64(value, ref actual, offset, useNbo);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5, 3, new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00})]
        public void Test_FromUInt64_NoNboSpecified(ulong value, int offset, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[expected.Length];
            converter.FromUInt64(value, actual, offset);

            Assert.Equal(expected, actual);
        }

        #endregion

        #region bits

        [Theory]
        [InlineData(0x00, 0, false)]
        [InlineData(0xff, 0, true)]
        [InlineData(0x01, 0, true)]
        [InlineData(0x80, 7, true)]
        [InlineData(0xf7, 3, false)]
        public void Test_GetBit(byte value, int position, bool expected)
        {
            var converter = new DefaultConverter();
            var actual = converter.GetBit(value, position);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(0x00, 0, false, 0x00)]
        [InlineData(0xff, 0, true, 0xff)]
        [InlineData(0x01, 0, true, 0x01)]
        [InlineData(0x80, 7, true, 0x80)]
        [InlineData(0xf7, 3, false, 0xf7)]
        [InlineData(0xff, 0, false, 0xfe)]
        [InlineData(0x00, 0, true, 0x01)]
        [InlineData(0xfe, 0, true, 0xff)]
        [InlineData(0x80, 3, true, 0x88)]
        [InlineData(0xff, 3, false, 0xf7)]
        public void Test_SetBit(byte initialValue, int position, bool bit, byte expected)
        {
            var converter = new DefaultConverter();
            var actual = initialValue;
            converter.SetBit(ref actual, position, bit);

            Assert.Equal(expected, actual);
        }

        #endregion
    }
}
