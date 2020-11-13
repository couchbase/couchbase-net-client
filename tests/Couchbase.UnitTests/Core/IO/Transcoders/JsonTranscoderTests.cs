using System;
using System.IO;
using System.Text;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Transcoders
{
    public class JsonTranscoderTests
    {
        [Fact]
        public void When_ByteArray_Decoded_Throw_UnsupportedException()
        {
            var transcoder = new JsonTranscoder(new DefaultSerializer());

            var bytes = new byte[0];

            Assert.Throws<UnsupportedException>(()=> transcoder.Decode<byte[]>(bytes.AsMemory(),
                new Flags {DataFormat = DataFormat.Binary},
                OpCode.NoOp));
        }

        [Fact]
        public void When_ByteArray_Encoded_Throw_UnsupportedException()
        {
            var transcoder = new JsonTranscoder(new DefaultSerializer());

            var bytes = new byte[0];

            Assert.Throws<UnsupportedException>(() => transcoder.Decode<byte[]>(bytes.AsMemory(),
                new Flags { DataFormat = DataFormat.Binary },
                OpCode.NoOp));
        }


        [Fact]
        public void DecodeString_Defaults_To_Null_When_Buffer_Is_Empty_And_Type_Is_String()
        {
            var transcoder = new JsonTranscoder(new DefaultSerializer());

            var bytes = new byte[0];
            var result = transcoder.Decode<string>(bytes.AsMemory(),
                new Flags { DataFormat = DataFormat.String },
                OpCode.NoOp);

            Assert.Null(result);
        }

        [Fact]
        public void DecodeString_Defaults_To_Null_When_Buffer_Is_Empty_And_Type_Is_JSON()
        {
            var transcoder = new JsonTranscoder(new DefaultSerializer());

            var bytes = new byte[0];
            var result = transcoder.Decode<string>(bytes.AsMemory(),
                new Flags { DataFormat = DataFormat.Json },
                OpCode.NoOp);

            Assert.Null(result);
        }

        [Fact]
        public void DecodeString_Returns_String_When_Buffer_Is_Empty_And_Type_Is_String()
        {
            var transcoder = new JsonTranscoder(new DefaultSerializer());

            var bytes = new byte[0];
            var result = transcoder.Decode<string>(bytes.AsMemory(),
                new Flags { DataFormat = DataFormat.String },
                OpCode.NoOp);

            Assert.Null(result);
        }

        [Fact]
        public void Test_Serialize_Int16()
        {
            var transcoder = new JsonTranscoder();
            Int16 data = 5;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Json,
                TypeCode = Convert.GetTypeCode(data)
            };

            var expected = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));

            using var stream = new MemoryStream();
            transcoder.Encode(stream, data, flags, OpCode.Get);

            Assert.Equal(expected, stream.ToArray());
        }

        [Fact]
        public void Test_Serialize_UInt16()
        {
            var transcoder = new JsonTranscoder(new DefaultSerializer());
            UInt16 data = 5;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Json,
                TypeCode = Convert.GetTypeCode(data)
            };

            var expected = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));

            using var stream = new MemoryStream();
            transcoder.Encode(stream, data, flags, OpCode.Get);

            Assert.Equal(expected, stream.ToArray());
        }

        [Fact]
        public void Test_Serialize_Int32()
        {
            var transcoder = new JsonTranscoder();
            Int32 data = 9;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Json,
                TypeCode = Convert.GetTypeCode(data)
            };

            var expected = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));
            using var stream = new MemoryStream();
            transcoder.Encode(stream, data, flags, OpCode.Get);

            Assert.Equal(expected, stream.ToArray());
        }

        [Fact]
        public void Test_Serialize_UInt32()
        {
            var transcoder = new JsonTranscoder();
            UInt32 data = 9;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Json,
                TypeCode = Convert.GetTypeCode(data)
            };

            var expected = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));
            using var stream = new MemoryStream();
            transcoder.Encode(stream, data, flags, OpCode.Get);

            Assert.Equal(expected, stream.ToArray());
        }

        [Fact]
        public void Test_Serialize_Int64()
        {
            var transcoder = new JsonTranscoder();
            Int64 data = 9;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Json,
                TypeCode = Convert.GetTypeCode(data)
            };

            var expected = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));
            using var stream = new MemoryStream();
            transcoder.Encode(stream, data, flags, OpCode.Get);

            Assert.Equal(expected, stream.ToArray());
        }

        [Fact]
        public void Test_Serialize_UInt64()
        {
            var transcoder = new JsonTranscoder();
            UInt64 data = 9;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Json,
                TypeCode = Convert.GetTypeCode(data)
            };

            var expected = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));
            using var stream = new MemoryStream();
            transcoder.Encode(stream, data, flags, OpCode.Get);

            Assert.Equal(expected, stream.ToArray());
        }

        [Fact]
        public void Test_Serialize_String()
        {
            var transcoder = new JsonTranscoder();
            string data = "Hello";

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Json,
                TypeCode = Convert.GetTypeCode(data)
            };

            var expected = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));
            using var stream = new MemoryStream();
            transcoder.Encode(stream, data, flags, OpCode.Get);

            Assert.Equal(expected, stream.ToArray());
        }

        [Fact]
        public void Test_Null()
        {
            var transcoder = new JsonTranscoder();

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Json,
                TypeCode = TypeCode.Empty
            };

            var expected = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(null));
            using var stream = new MemoryStream();
            transcoder.Encode<string>(stream, null, flags, OpCode.Get);

            Assert.Equal(expected, stream.ToArray());
        }

        [Fact]
        public void Test_Char()
        {
            var transcoder = new JsonTranscoder();
            var value = 'o';

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Json,
                TypeCode = Convert.GetTypeCode(value)
            };

            var json = JsonConvert.SerializeObject(value);
            var expected = Encoding.UTF8.GetBytes(json);
            using var stream = new MemoryStream();
            transcoder.Encode(stream, value, flags, OpCode.Get);

            Assert.Equal(expected, stream.ToArray());
        }

        [Fact]
        // ReSharper disable once IdentifierTypo
        public void Test_Poco()
        {
            var transcoder = new JsonTranscoder();
            var value = new Person { Name = "jeff" };

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Json,
                TypeCode = Type.GetTypeCode(typeof(Person))
            };

            using var stream = new MemoryStream();
            transcoder.Encode(stream, value, flags, OpCode.Get);
            var actual = transcoder.Decode<Person>(stream.ToArray(), flags, OpCode.Get);

            Assert.Equal(value.Name, actual.Name);
        }

        [Fact]
        public void Test_Deserialize_Int()
        {
            var transcoder = new JsonTranscoder();
            var five = 5;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Json,
                TypeCode = Convert.GetTypeCode(five)
            };

            using var stream = new MemoryStream();
            transcoder.Encode(stream, five, flags, OpCode.Get);
            var actual = transcoder.Decode<int>(stream.ToArray(), flags, OpCode.Get);
            Assert.Equal(five, actual);
        }

        [Fact]
        public void Test_Deserialize_Null()
        {
            var transcoder = new JsonTranscoder();
            object value = null;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Json,
                TypeCode = TypeCode.Empty
            };

            using var stream = new MemoryStream();
            // ReSharper disable once ExpressionIsAlwaysNull
            transcoder.SerializeAsJson(stream, value);
            var actual = transcoder.Decode<object>(stream.ToArray(), flags, OpCode.Get);
            Assert.Equal(value, actual);
        }

        [Fact]
        public void Test_Deserialize_String()
        {
            var transcoder = new JsonTranscoder();
            // ReSharper disable once StringLiteralTypo
            var value = "astring";

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Json,
                TypeCode = Convert.GetTypeCode(value)
            };

            using var stream = new MemoryStream();
            transcoder.Encode(stream, value, flags, OpCode.Get);
            var actual = transcoder.Decode<string>(stream.ToArray(), flags, OpCode.Get);
            Assert.Equal(value, actual);
        }

        [Fact]
        public void Test_Deserialize_JsonString()
        {
            var transcoder = new JsonTranscoder();
            // ReSharper disable once StringLiteralTypo
            var value = "{\"name\":\"astring\"}";

            var json = JsonConvert.SerializeObject(new Hmm
            {
                name = "astring"
            });

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Json,
                TypeCode = Convert.GetTypeCode(value)
            };

            using var stream = new MemoryStream();
            transcoder.Encode(stream, json, flags, OpCode.Get);
            var actual = transcoder.Decode<string>(stream.ToArray(), flags, OpCode.Get);
            Assert.Equal(value, actual);
        }

        public class Hmm
        {
            public string name { get; set; }
        }

        [Fact]
        public void Test_Deserialize_Char()
        {
            var transcoder = new JsonTranscoder();
            var value = 'o';

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Binary,
                TypeCode = Convert.GetTypeCode(value)
            };

            var expected = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value));
            var actual = transcoder.Decode<char>(expected.AsMemory(), flags, OpCode.Get);
            Assert.Equal(value, actual);
        }

        public class Person
        {
            public string Name { get; set; }
        }

        [Fact]
        public void Test_Json_Deserialize_Int()
        {
            var transcoder = new JsonTranscoder();
            int value = 42;

            using var stream = new MemoryStream();
            transcoder.SerializeAsJson(stream, value);
            var actual = transcoder.DeserializeAsJson<int>(stream.ToArray());

            Assert.Equal(value, actual);
        }

        [Fact]
        // ReSharper disable once IdentifierTypo
        public void Should_Hydrate_Poco_In_PascalCase_Whatever_The_Case_In_Json()
        {
            var jsonData = Encoding.UTF8.GetBytes("{ \"SomeProperty\": \"SOME\", \"someIntProperty\": 12345, \"haspAscalCASE\": true }");
            var transcoder = new JsonTranscoder();
            var hydrated = transcoder.DeserializeAsJson<Pascal>(jsonData.AsMemory());

            Assert.Equal("SOME", hydrated.SomeProperty);
            Assert.Equal(12345, hydrated.SomeIntProperty);
            Assert.True(hydrated.HasPascalCase);
        }

        [Fact]
        public void Should_Convert_To_CamelCase_Json_With_Default_Serialization_Settings()
        {
            var transcoder = new JsonTranscoder();
            var data = new Transcoders.Pascal
            {
                SomeProperty = "SOME",
                SomeIntProperty = 12345,
                HasPascalCase = true
            };
            var expectedJsonBytes = Encoding.UTF8.GetBytes("{\"someProperty\":\"SOME\",\"someIntProperty\":12345,\"hasPascalCase\":true}");

            using var jsonBytes = new MemoryStream();
            using var jsonEncoded = new MemoryStream();
            transcoder.SerializeAsJson(jsonBytes, data);
            transcoder.Encode(jsonEncoded, data, new Flags { DataFormat = DataFormat.Json }, OpCode.Get);
            Assert.Equal(expectedJsonBytes, jsonBytes.ToArray());
            Assert.Equal(expectedJsonBytes, jsonEncoded.ToArray());
        }

        [Fact]
        public void Should_Convert_To_PascalCase_Json_With_Altered_Serialization_Settings()
        {
            var transcoder = new JsonTranscoder(
                new DefaultSerializer(
                    new JsonSerializerSettings(),
                    new JsonSerializerSettings
                    {
                        ContractResolver = new DefaultContractResolver()
                    }));

            var data = new Transcoders.Pascal
            {
                SomeProperty = "SOME",
                SomeIntProperty = 12345,
                HasPascalCase = true
            };
            var expectedJsonBytes = Encoding.UTF8.GetBytes("{\"SomeProperty\":\"SOME\",\"SomeIntProperty\":12345,\"HasPascalCase\":true}");

            using var jsonBytes = new MemoryStream();
            using var jsonEncoded = new MemoryStream();
            transcoder.SerializeAsJson(jsonBytes, data);
            transcoder.Encode(jsonEncoded, data, new Flags
            {
                DataFormat = DataFormat.Json
            }, OpCode.Get);

            Assert.Equal(expectedJsonBytes, jsonBytes.ToArray());
            Assert.Equal(expectedJsonBytes, jsonEncoded.ToArray());
        }

        class Pascal
        {
            public string SomeProperty { get; set; }

            public int SomeIntProperty { get; set; }

            public bool HasPascalCase { get; set; }
        }
    }
}
