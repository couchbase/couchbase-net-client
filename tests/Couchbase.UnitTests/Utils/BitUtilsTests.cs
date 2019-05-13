using System;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class BitUtilsTests
    {
        [Theory]
        [InlineData(0x00, 0, false)]
        [InlineData(0xff, 0, true)]
        [InlineData(0x01, 0, true)]
        [InlineData(0x80, 7, true)]
        [InlineData(0xf7, 3, false)]
        public void Test_GetBit(byte value, int position, bool expected)
        {
            var actual = BitUtils.GetBit(value, position);

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
            var actual = initialValue;
            BitUtils.SetBit(ref actual, position, bit);

            Assert.Equal(expected, actual);
        }
    }
}
