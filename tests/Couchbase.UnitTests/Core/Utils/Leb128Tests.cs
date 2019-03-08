using System;
using Couchbase.Core.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.UnitTests.Core.Utils
{
    public class Leb128Tests
    {
        private ITestOutputHelper _output;

        public Leb128Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData(0x00, new byte []{0x00})]
        [InlineData(0x01, new byte[] {0x01})]
        [InlineData(0x7F, new byte[] {0x7F})]
        [InlineData(0x80, new byte[] {0x80, 0x01})]
        [InlineData(0x555, new byte[] {0xD5, 0x0A})]
        [InlineData(0x7FFF, new byte[] {0xFF, 0xFF, 0x01})]
        [InlineData(0xBFFF, new byte[] {0xFF, 0xFF, 0x02})]
        [InlineData(0xFFFF, new byte[] {0XFF, 0xFF, 0x03})]
        [InlineData(0x8000, new byte[] {0x80, 0x80, 0x02})]
        [InlineData(0x5555, new byte[] {0xD5, 0xAA, 0x01})]
        [InlineData(0xCAFEF00, new byte[] {0x80, 0xDE, 0xBF, 0x65})]
        [InlineData(0xCAFEF00D, new byte[] {0x8D, 0xE0, 0xFB, 0xD7, 0x0C})]
        [InlineData(0xFFFFFFFF, new byte[] {0xFF, 0xFF, 0xFF, 0xFF, 0x0F})]
        public void Test_Write(uint value, byte[] expected)
        {
            var bytes = Leb128.Write(value);
            Assert.Equal(expected, bytes);

            var decoded = Leb128.Read(bytes);
            Assert.Equal(value, decoded);
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

            _output.WriteLine("{0} => {1}", value, cid);
            Assert.Equal(cid <= 1000, lessThan);
        }
        
        [Theory]
        [InlineData(new byte[]{ 0xab, 0x04}, 555u)]
        public void Test_Read(byte[] bytes, uint expected)
        {
            var actual = Leb128.Read(bytes);
            Assert.Equal(expected, actual);
        }
    }
}

