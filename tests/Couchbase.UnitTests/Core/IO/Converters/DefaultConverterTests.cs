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

            var actual = converter.ToBoolean(bytes.AsSpan(), useNbo);

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

            var actual = converter.ToDouble(bytes.AsSpan(), useNbo);

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

            var actual = converter.ToSingle(bytes.AsSpan(), useNbo);

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

            var actual = converter.ToDateTime(bytes.AsSpan(), useNbo);

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

            var actual = converter.ToInt16(bytes.AsSpan(), useNbo);

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

            var actual = converter.ToInt16(bytes.AsSpan());

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

            var actual = converter.ToUInt16(bytes.AsSpan(), useNbo);

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

            var actual = converter.ToUInt16(bytes.AsSpan());

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

            var actual = converter.ToInt32(bytes.AsSpan(), useNbo);

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

            var actual = converter.ToInt32(bytes.AsSpan());

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

            var actual = converter.ToUInt32(bytes.AsSpan(), useNbo);

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

            var actual = converter.ToUInt32(bytes.AsSpan());

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
            var actual = converter.ToUInt64(bytes.AsSpan(), useNbo);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(new byte[] {0xff, 0xff, 0xff, 0xff, 0xe5, 0x5d, 0x9f, 0xdf}, 18446744073262702559)]
        [InlineData(new byte[] {0x00, 0x00, 0xa9, 0x53, 0x5f, 0x3d, 0xa7, 0x0f}, 186175545255695)]
        public void Test_ToUInt64_NoNboSpecified(byte[] bytes, ulong expected)
        {
            var converter = new DefaultConverter();
            var actual = converter.ToUInt64(bytes.AsSpan());
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
            var actual = converter.ToInt64(bytes.AsSpan(), useNbo);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(new byte[] {0x7f, 0xff, 0xff, 0xff, 0x5f, 0x3d, 0xa7, 0x0f}, 9223372034157684495)]
        [InlineData(new byte[] {0x00, 0x00, 0xa9, 0x53, 0x5f, 0x3d, 0xa7, 0x0f}, 186175545255695)]
        public void Test_ToInt64_NoNboSpecified(byte[] bytes, long expected)
        {
            var converter = new DefaultConverter();
            var actual = converter.ToInt64(bytes.AsSpan());
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(new byte[] {0x0, 0x0, 0x48, 0x65, 0x6c, 0x6c, 0x6f, 0x92}, "Hello", 2, 5)]
        [InlineData(new byte[] {0x48, 0x65, 0x6c, 0x6c, 0x6f}, "Hello", 0, 5)]
        public void Test_ToString(byte[] bytes, string expected, int offset, int length)
        {
            var converter = new DefaultConverter();
            var actual = converter.ToString(bytes.AsSpan(offset, length));

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
            converter.FromString(value, buffer.AsSpan(offset));

            // Buffer matches expected
            Assert.Equal(expected, buffer.Take(expected.Length));

            // Remainder of buffer is still zeroes
            Assert.All(buffer.Skip(expected.Length), p => Assert.Equal(0, p));
        }

        [Theory]
        [InlineData(5, 3, true, new byte[] {0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00})]
        [InlineData(5, 0, false, new byte[] {0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})]
        public void Test_FromInt16(short value, int offset, bool useNbo, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[8];
            converter.FromInt16(value, actual.AsSpan(offset), useNbo);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5, 3, new byte[] {0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00})]
        public void Test_FromInt16_NoNboSpecified(short value, int offset, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[8];
            converter.FromInt16(value, actual.AsSpan(offset));

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5, 3, true, new byte[] {0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00})]
        [InlineData(5, 0, false, new byte[] {0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})]
        public void Test_FromUInt16(ushort value, int offset, bool useNbo, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[8];
            converter.FromUInt16(value, actual.AsSpan(offset), useNbo);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5, 3, new byte[] {0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00})]
        public void Test_FromUInt16_NoNboSpecified(ushort value, int offset, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[8];
            converter.FromUInt16(value, actual.AsSpan(offset));

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5, 3, true, new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00})]
        [InlineData(5, 0, false, new byte[] {0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})]
        public void Test_FromInt32(int value, int offset, bool useNbo, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[11];
            converter.FromInt32(value, actual.AsSpan(offset), useNbo);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5, 3, new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00})]
        public void Test_FromInt32_NoNboSpecified(int value, int offset, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[11];
            converter.FromInt32(value, actual.AsSpan(offset));

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5u, 3, true, new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00})]
        [InlineData(5u, 0, false, new byte[] {0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})]
        public void Test_FromUInt32(uint value, int offset, bool useNbo, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[11];
            converter.FromUInt32(value, actual.AsSpan(offset), useNbo);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5u, 3, new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00})]
        public void Test_FromUInt32_NoNboSpecified(uint value, int offset, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[11];
            converter.FromUInt32(value, actual.AsSpan(offset));

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5, 3, true, new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00})]
        [InlineData(5, 0, false, new byte[] {0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})]
        public void Test_FromInt64(long value, int offset, bool useNbo, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[expected.Length];
            converter.FromInt64(value, actual.AsSpan(offset), useNbo);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5, 3, new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00})]
        public void Test_FromInt64_NoNboSpecified(long value, int offset, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[expected.Length];
            converter.FromInt64(value, actual.AsSpan(offset));

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5, 3, true, new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00})]
        [InlineData(5, 0, false, new byte[] {0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00})]
        public void Test_FromUInt64(ulong value, int offset, bool useNbo, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[expected.Length];
            converter.FromUInt64(value, actual.AsSpan(offset), useNbo);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(5, 3, new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00})]
        public void Test_FromUInt64_NoNboSpecified(ulong value, int offset, byte[] expected)
        {
            var converter = new DefaultConverter();
            var actual = new byte[expected.Length];
            converter.FromUInt64(value, actual.AsSpan(offset));

            Assert.Equal(expected, actual);
        }

        #endregion
    }
}
