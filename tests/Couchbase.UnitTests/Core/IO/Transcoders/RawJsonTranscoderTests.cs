using System;
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
        public void Test_GetFormat_Object_Fails()
        {
            var transcoder = new RawJsonTranscoder();

            Assert.Throws<InvalidOperationException>(() => transcoder.GetFormat(new object()));
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
    }
}
