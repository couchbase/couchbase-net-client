using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;
using Newtonsoft.Json;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Transcoders
{
    public class RawStringTranscoderTests
    {
        [Fact]
        public void DecodeString_Defaults_To_Null_When_Buffer_Is_Empty_And_Type_Is_String()
        {
            var transcoder = new RawStringTranscoder();

            var bytes = new byte[0];
            var result = transcoder.Decode<string>(bytes.AsMemory(),
                new Flags { DataFormat = DataFormat.String },
                OpCode.NoOp);

            Assert.Null(result);
        }

        [Fact]
        public void Test_Serialize_String()
        {
            var transcoder = new RawStringTranscoder();
            string data = "Hello";

            var flags = new Flags
            {
                Compression = Couchbase.Core.IO.Operations.Compression.None,
                DataFormat = DataFormat.String,
                TypeCode = Convert.GetTypeCode(data)
            };

            var expected = new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f };
            using var stream = new MemoryStream();
            transcoder.Encode(stream, data, flags, OpCode.Get);

            Assert.Equal(expected, stream.ToArray());
        }

        [Fact]
        public void Test_Char_Fails()
        {
            var transcoder = new RawStringTranscoder();
            var value = 'o';

            var flags = new Flags
            {
                Compression = Couchbase.Core.IO.Operations.Compression.None,
                DataFormat = DataFormat.String,
                TypeCode = Convert.GetTypeCode(value)
            };

            using var stream = new MemoryStream();
            Assert.Throws<InvalidOperationException>(()=>transcoder.Encode(stream, value, flags, OpCode.Get));
        }

        [Fact]
        public void Test_Deserialize_String()
        {
            var transcoder = new RawStringTranscoder();
            // ReSharper disable once StringLiteralTypo
            var value = "astring";

            var flags = new Flags
            {
                Compression = Couchbase.Core.IO.Operations.Compression.None,
                DataFormat = DataFormat.String,
                TypeCode = Convert.GetTypeCode(value)
            };

            using var stream = new MemoryStream();
            transcoder.Encode(stream, value, flags, OpCode.Get);
            var actual = transcoder.Decode<string>(stream.ToArray(), flags, OpCode.Get);
            Assert.Equal(value, actual);
        }

        [Fact]
        public void Test_Encode_Object_Fails()
        {
            var transcoder = new RawStringTranscoder();

            var flags = new Flags
            {
                DataFormat = DataFormat.String
            };

            using var stream = new MemoryStream();
            Assert.Throws<InvalidOperationException>(() => transcoder.Encode(stream, new object(), flags, OpCode.Add));
        }

        [Fact]
        public void Test_Decode_Object_Fails()
        {
            var transcoder = new RawStringTranscoder();

            var flags = new Flags
            {
                DataFormat = DataFormat.Json
            };

            var memory = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject("Hello, world!")));
            Assert.Throws<InvalidOperationException>(() => transcoder.Decode<object>(memory, flags, OpCode.NoOp));
        }

        [Fact]
        public void Test_GetFormat_Object_Fails()
        {
            var transcoder = new RawStringTranscoder();

            Assert.Throws<InvalidOperationException>(() => transcoder.GetFormat(new object()));
        }
    }
}
