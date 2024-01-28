using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.Utils
{
    public class Leb128Tests(ITestOutputHelper output)
    {
        public static IEnumerable<object[]> TestData()
        {
            yield return [0x00, new byte[] { 0x00 }];
            yield return [0x01, new byte[] { 0x01 }];
            yield return [0x7F, new byte[] { 0x7F }];
            yield return [0x80, new byte[] { 0x80, 0x01 }];
            yield return [0x555, new byte[] { 0xD5, 0x0A }];
            yield return [0x7FFF, new byte[] { 0xFF, 0xFF, 0x01 }];
            yield return [0xBFFF, new byte[] { 0xFF, 0xFF, 0x02 }];
            yield return [0xFFFF, new byte[] { 0XFF, 0xFF, 0x03 }];
            yield return [0x8000, new byte[] { 0x80, 0x80, 0x02 }];
            yield return [0x5555, new byte[] { 0xD5, 0xAA, 0x01 }];
            yield return [0xCAFEF00, new byte[] { 0x80, 0xDE, 0xBF, 0x65 }];
            yield return [0xCAFEF00D, new byte[] { 0x8D, 0xE0, 0xFB, 0xD7, 0x0C }];
            yield return [0xFFFFFFFF, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F }];
        }

        [Theory]
        [MemberData(nameof(TestData))]
        public void Test_Write(uint value, byte[] expected)
        {
            var bytes = new byte[5];

            var length = Leb128.Write(bytes, value);
            Assert.Equal(expected, bytes.Take(length));
        }

        [Theory]
        [MemberData(nameof(TestData))]
        public void Test_WriteWithPadding(uint value, byte[] expected)
        {
            // Test of the fast path where there are at least 8 bytes in the buffer

            var bytes = new byte[sizeof(ulong)];

            var length = Leb128.Write(bytes, value);
            Assert.Equal(expected, bytes.Take(length));
        }

        [Theory]
        [InlineData("5612a", false)]
        [InlineData("0", true)]
        [InlineData("1b", true)]
        [InlineData("3356", false)]
        [InlineData("80a1", false)]
        public void Parse(string value, bool lessThan)
        {
            var cid = Convert.ToUInt32(value, 16);

            output.WriteLine("{0} => {1}", value, cid);
            Assert.Equal(cid <= 1000, lessThan);
        }

        [Theory]
        [MemberData(nameof(TestData))]
        public void Test_Read(uint expected, byte[] bytes)
        {
            var actual = Leb128.Read(bytes);
            Assert.Equal(expected, actual.Item1);
        }
    }
}

