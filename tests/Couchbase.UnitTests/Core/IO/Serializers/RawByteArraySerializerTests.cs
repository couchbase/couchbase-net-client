using System;
using System.IO;
using System.Text;
using Couchbase.Core.IO.Serializers;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Serializers
{
    public class RawByteArraySerializerTests
    {
        [Fact]
        public void Constructor_NullInner_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new RawByteArraySerializer(null!));
        }

        [Fact]
        public void Deserialize_ByteArray_ReturnsRawBytes_NotBase64Decoded()
        {
            // RawByteArraySerializer passes byte[] through raw, unlike the default serializer which
            // treats a byte[] as a Base64 JSON string. Given the raw JSON bytes of a sub-document
            // fragment, Deserialize<byte[]> returns those exact bytes -- the cross-SDK
            // contentAs(byte[]) contract.
            var serializer = new RawByteArraySerializer(new DefaultSerializer());
            var raw = Encoding.UTF8.GetBytes("{\"content\":\"initial\"}");

            var result = serializer.Deserialize<byte[]>(new ReadOnlyMemory<byte>(raw));

            Assert.Equal(raw, result);
        }

        [Fact]
        public void Serialize_ByteArray_WritesRawBytes()
        {
            var serializer = new RawByteArraySerializer(new DefaultSerializer());
            var original = new byte[] { 10, 36, 102, 50, 56, 98, 49, 51 };

            using var stream = new MemoryStream();
            serializer.Serialize(stream, original);

            // Raw bytes, not a quoted Base64 string.
            Assert.Equal(original, stream.ToArray());
        }

        [Fact]
        public void Deserialize_NonByteArray_DelegatesToInner()
        {
            var serializer = new RawByteArraySerializer(new DefaultSerializer());
            var json = Encoding.UTF8.GetBytes("\"hello\"");

            var result = serializer.Deserialize<string>(new ReadOnlyMemory<byte>(json));

            Assert.Equal("hello", result);
        }

        [Fact]
        public void Serialize_NonByteArray_DelegatesToInner()
        {
            var serializer = new RawByteArraySerializer(new DefaultSerializer());

            using var stream = new MemoryStream();
            serializer.Serialize(stream, "hello");

            Assert.Equal("\"hello\"", Encoding.UTF8.GetString(stream.ToArray()));
        }
    }
}
