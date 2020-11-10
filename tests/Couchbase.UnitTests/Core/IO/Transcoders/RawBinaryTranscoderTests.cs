using System;
using System.IO;
using System.Text;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Transcoders
{
    public class RawBinaryTranscoderTests
    {
        [Fact]
        public void Test_Encode_ByteArrays()
        {
            var transcoder = new RawBinaryTranscoder();

            var flags = new Flags
            {
                DataFormat = DataFormat.Binary
            };

            using var stream = new MemoryStream();
            transcoder.Encode(stream, new byte[]{0x00, 0x01}, flags, OpCode.Add);

            Assert.True(stream.Length == 2);
        }


        [Fact]
        public void Test_Encode_Object_Fails()
        {
            var transcoder = new RawBinaryTranscoder();

            var flags = new Flags
            {
                DataFormat = DataFormat.Binary
            };

            using var stream = new MemoryStream();
            Assert.Throws<InvalidOperationException>(()=>transcoder.Encode(stream, new object(), flags, OpCode.Add));
        }

        [Theory]
        [InlineData(DataFormat.String)]
        [InlineData(DataFormat.Binary)]
        [InlineData(DataFormat.Json)]
        [InlineData(DataFormat.Private)]
        [InlineData(DataFormat.Reserved)]
        public void Test_Decode_ByteArrays(DataFormat dataFormat)
        {
            var transcoder = new RawBinaryTranscoder();

            var flags = new Flags
            {
                DataFormat = dataFormat //Note flags type is independent of T - everything is byte[]
            };

            var memory = new ReadOnlyMemory<byte>(new byte[]{0x0, 0x1});
            var bytes = transcoder.Decode<byte[]>(memory, flags, OpCode.NoOp);

            Assert.True(bytes.Length == 2);
        }

        [Fact]
        public void Test_Decode_Object_Fails()
        {
            var transcoder = new RawBinaryTranscoder();

            var flags = new Flags
            {
                DataFormat = DataFormat.String
            };

            var memory = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes("Hello, world!"));
            Assert.Throws<InvalidOperationException>(()=> transcoder.Decode<object>(memory, flags, OpCode.NoOp));
        }

        [Fact]
        public void Test_GetFormat_Binary_Succeeds()
        {
            var transcoder = new RawBinaryTranscoder();

            var flags = transcoder.GetFormat(new byte[] {0x0});

            Assert.Equal(DataFormat.Binary, flags.DataFormat);
        }

        [Fact]
        public void Test_GetFormat_Object_Fails()
        {
            var transcoder = new RawBinaryTranscoder();

            Assert.Throws<InvalidOperationException>(()=>transcoder.GetFormat(new object()));
        }
    }
}
