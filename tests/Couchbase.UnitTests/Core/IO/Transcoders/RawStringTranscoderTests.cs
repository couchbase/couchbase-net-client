using System;
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
        #region GetFormat

        [Fact]
        public void Test_GetFormat_Object_Fails()
        {
            var transcoder = new RawStringTranscoder();

            Assert.Throws<InvalidOperationException>(() => transcoder.GetFormat(new object()));
        }

        [Fact]
        public void Test_GetFormat_String_String()
        {
            var transcoder = new RawStringTranscoder();

            var flags = transcoder.GetFormat("12");

            Assert.Equal(DataFormat.String, flags.DataFormat);
        }

        #endregion

        #region Encode

        [Theory]
        [InlineData(100)]
        [InlineData(10_000)]
        public void Test_Encode_String(int length)
        {
            var transcoder = new RawStringTranscoder();

            var flags = new Flags
            {
                DataFormat = DataFormat.String
            };

            var str = new string('0', length);
            using var stream = new MemoryStream();
            transcoder.Encode(stream, str, flags, OpCode.NoOp);

            Assert.Equal(stream.ToArray(), Encoding.UTF8.GetBytes(str));
        }

        [Fact]
        public void Test_Encode_Char_Fails()
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

        #endregion

        #region Decode

        [Fact]
        public void DecodeString_Defaults_To_Null_When_Buffer_Is_Empty_And_Type_Is_String()
        {
            var transcoder = new RawStringTranscoder();

            var bytes = Array.Empty<byte>();
            var result = transcoder.Decode<string>(bytes.AsMemory(),
                new Flags { DataFormat = DataFormat.String },
                OpCode.NoOp);

            Assert.Null(result);
        }

        [Fact]
        public void Test_Decode_String()
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

        #endregion
    }
}
