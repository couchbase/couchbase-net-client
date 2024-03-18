using System;
using System.Buffers;
using System.IO;
using System.Text;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Transcoders
{
    public class RawJsonTranscoderTests
    {
        #region GetFormat

        [Fact]
        public void Test_GetFormat_Object_Fails()
        {
            var transcoder = new RawJsonTranscoder();

            Assert.Throws<InvalidOperationException>(() => transcoder.GetFormat(new object()));
        }

        [Fact]
        public void Test_GetFormat_ByteArray_Json()
        {
            var transcoder = new RawJsonTranscoder();

            var flags = transcoder.GetFormat(new byte[] { 1, 2 });

            Assert.Equal(DataFormat.Json, flags.DataFormat);
        }

        [Fact]
        public void Test_GetFormat_Memory_Json()
        {
            var transcoder = new RawJsonTranscoder();

            var flags = transcoder.GetFormat((Memory<byte>) new byte[] { 1, 2 });

            Assert.Equal(DataFormat.Json, flags.DataFormat);
        }

        [Fact]
        public void Test_GetFormat_ReadOnlyMemory_Json()
        {
            var transcoder = new RawJsonTranscoder();

            var flags = transcoder.GetFormat((ReadOnlyMemory<byte>) new byte[] { 1, 2 });

            Assert.Equal(DataFormat.Json, flags.DataFormat);
        }

        [Fact]
        public void Test_GetFormat_String_Json()
        {
            var transcoder = new RawJsonTranscoder();

            var flags = transcoder.GetFormat("12");

            Assert.Equal(DataFormat.Json, flags.DataFormat);
        }

        #endregion

        #region Encode

        [Fact]
        public void Test_Encode_Object_Fails()
        {
            var transcoder = new RawJsonTranscoder();

            var flags = new Flags
            {
                DataFormat = DataFormat.Json
            };

            using var stream = new MemoryStream();
            Assert.Throws<InvalidOperationException>(() => transcoder.Encode(stream, new object(), flags, OpCode.Add));
        }
        [Fact]
        public void Test_Encode()
        {
            var transcoder = new RawJsonTranscoder();

            var flags = new Flags
            {
                DataFormat = DataFormat.Json
            };

            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new {name = "fred", age = 45}));
            using var stream = new MemoryStream();
            transcoder.Encode(stream, bytes, flags, OpCode.NoOp);

            Assert.Equal(stream.ToArray(), bytes);
        }

        [Fact]
        public void Test_Encode_Memory()
        {
            var transcoder = new RawJsonTranscoder();

            var flags = new Flags
            {
                DataFormat = DataFormat.Json
            };

            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new {name = "fred", age = 45}));
            using var stream = new MemoryStream();
            transcoder.Encode(stream, (Memory<byte>)bytes, flags, OpCode.NoOp);

            Assert.Equal(stream.ToArray(), bytes);
        }

        [Fact]
        public void Test_Encode_ReadOnlyMemory()
        {
            var transcoder = new RawJsonTranscoder();

            var flags = new Flags
            {
                DataFormat = DataFormat.Json
            };

            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new {name = "fred", age = 45}));
            using var stream = new MemoryStream();
            transcoder.Encode(stream, (Memory<byte>)bytes, flags, OpCode.NoOp);

            Assert.Equal(stream.ToArray(), bytes);
        }

        [Theory]
        [InlineData(100)]
        [InlineData(10_000)]
        public void Test_Encode_String(int length)
        {
            var transcoder = new RawJsonTranscoder();

            var flags = new Flags
            {
                DataFormat = DataFormat.Json
            };

            var str = new string('0', length);
            using var stream = new MemoryStream();
            transcoder.Encode(stream, str, flags, OpCode.NoOp);

            Assert.Equal(stream.ToArray(), Encoding.UTF8.GetBytes(str));
        }

        #endregion

        #region Decode

        [Fact]
        public void Test_Decode_Object_Fails()
        {
            var transcoder = new RawJsonTranscoder();

            var flags = new Flags
            {
                DataFormat = DataFormat.Binary
            };

            var memory = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject("Hello, world!")));
            Assert.Throws<InvalidOperationException>(() => transcoder.Decode<object>(memory, flags, OpCode.NoOp));
        }

        [Fact]
        public void Test_Decode_ByteArrays()
        {
            var transcoder = new RawJsonTranscoder();

            var flags = new Flags
            {
                DataFormat = DataFormat.Json
            };

            var memory = new ReadOnlyMemory<byte>(new byte[]{0x0, 0x1});
            var bytes = transcoder.Decode<byte[]>(memory, flags, OpCode.NoOp);

            Assert.True(bytes.Length == 2);
        }

        [Fact]
        public void Test_Decode_MemoryOwner()
        {
            var transcoder = new RawJsonTranscoder();

            var flags = new Flags
            {
                DataFormat = DataFormat.Json
            };

            var memory = new ReadOnlyMemory<byte>(new byte[]{0x0, 0x1});
            using var bytes = transcoder.Decode<IMemoryOwner<byte>>(memory, flags, OpCode.NoOp);

            Assert.True(bytes.Memory.Length == 2);
        }

        [Fact]
        public void Test_Decode_String()
        {
            var transcoder = new RawJsonTranscoder();

            var flags = new Flags
            {
                DataFormat = DataFormat.Json
            };

            var str = transcoder.Decode<string>("123456"u8.ToArray(), flags, OpCode.NoOp);

            Assert.Equal("123456", str);
        }

        #endregion
    }
}
