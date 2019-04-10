using System;
using System.IO;
using System.Text;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Legacy;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Transcoders
{
    public class DefaultTranscoderTests
    {
        [Fact]
        public void DecodeString_Defaults_To_Null_When_Buffer_Is_Empty_And_Type_Is_String()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter(), new DefaultSerializer());

            var bytes = new byte[0];
            var result = transcoder.Decode<string>(bytes.AsMemory(),
                new Flags {DataFormat = DataFormat.String},
                OpCode.NoOp);

            Assert.Null(result);
        }

        [Fact]
        public void DecodeString_Defaults_To_Null_When_Buffer_Is_Empty_And_Type_Is_JSON()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter(), new DefaultSerializer());

            var bytes = new byte[0];
            var result = transcoder.Decode<string>(bytes.AsMemory(),
                new Flags { DataFormat = DataFormat.Json },
                OpCode.NoOp);

            Assert.Null(result);
        }

        [Fact]
        public void DecodeString_Returns_String_When_Buffer_Is_Empty_And_Type_Is_String()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter(), new DefaultSerializer());

            var bytes = new byte[0];
            var result = transcoder.Decode<string>(bytes.AsMemory(),
                new Flags { DataFormat = DataFormat.String },
                OpCode.NoOp);

            Assert.Null(result);
        }

        [Fact]
        public void Test_Serialize_Int16()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter());
            Int16 data = 5;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = Convert.GetTypeCode(data)
            };

            var expected = new byte[] { 0x05, 0x00 };

            using (var stream = new MemoryStream())
            {
                transcoder.Encode(stream, data, flags, OpCode.Get);

                Assert.Equal(expected, stream.ToArray());
            }
        }

        [Fact]
        public void Test_Serialize_UInt16()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter(), new DefaultSerializer());
            UInt16 data = 5;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = Convert.GetTypeCode(data)
            };

            var expected = new byte[] { 0x05, 0x00 };
            using (var stream = new MemoryStream())
            {
                transcoder.Encode(stream, data, flags, OpCode.Get);

                Assert.Equal(expected, stream.ToArray());
            }
        }

        [Fact]
        public void Test_Serialize_Int32()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter());
            Int32 data = 9;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = Convert.GetTypeCode(data)
            };

            var expected = new byte[] { 0x09, 0x00, 0x00, 0x00 };
            using (var stream = new MemoryStream())
            {
                transcoder.Encode(stream, data, flags, OpCode.Get);

                Assert.Equal(expected, stream.ToArray());
            }
        }

        [Fact]
        public void Test_Serialize_UInt32()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter());
            UInt32 data = 9;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = Convert.GetTypeCode(data)
            };

            var expected = new byte[] { 0x09, 0x00, 0x00, 0x00 };
            using (var stream = new MemoryStream())
            {
                transcoder.Encode(stream, data, flags, OpCode.Get);

                Assert.Equal(expected, stream.ToArray());
            }
        }

        [Fact]
        public void Test_Serialize_Int64()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter());
            Int64 data = 9;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = Convert.GetTypeCode(data)
            };

            var expected = new byte[] { 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            using (var stream = new MemoryStream())
            {
                transcoder.Encode(stream, data, flags, OpCode.Get);

                Assert.Equal(expected, stream.ToArray());
            }
        }

        [Fact]
        public void Test_Serialize_UInt64()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter());
            UInt64 data = 9;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = Convert.GetTypeCode(data)
            };

            var expected = new byte[] { 0x09, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            using (var stream = new MemoryStream())
            {
                transcoder.Encode(stream, data, flags, OpCode.Get);

                Assert.Equal(expected, stream.ToArray());
            }
        }

        [Fact]
        public void Test_Serialize_String()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter());
            string data = "Hello";

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = Convert.GetTypeCode(data)
            };

            var expected = new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f };
            using (var stream = new MemoryStream())
            {
                transcoder.Encode(stream, data, flags, OpCode.Get);

                Assert.Equal(expected, stream.ToArray());
            }
        }

        [Fact]
        public void Test_Null()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter());

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = TypeCode.Empty
            };

            var expected = new byte[0];
            using (var stream = new MemoryStream())
            {
                transcoder.Encode<string>(stream, null, flags, OpCode.Get);

                Assert.Equal(expected, stream.ToArray());
            }
        }

        [Fact]
        public void Test_Char()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter());
            var value = 'o';

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = Convert.GetTypeCode(value)
            };

            var expected = new byte[] { 0x6f };
            using (var stream = new MemoryStream())
            {
                transcoder.Encode(stream, value, flags, OpCode.Get);

                Assert.Equal(expected, stream.ToArray());
            }
        }

        [Fact]
        public void Test_Poco()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter());
            var value = new Person { Name = "jeff" };

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = Type.GetTypeCode(typeof(Person))
            };

            using (var stream = new MemoryStream())
            {
                transcoder.Encode(stream, value, flags, OpCode.Get);
                var actual = transcoder.Decode<Person>(stream.ToArray(), flags, OpCode.Get);

                Assert.Equal(value.Name, actual.Name);
            }
        }

        [Fact]
        public void Test_Deserialize_Int()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter());
            var five = 5;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = Convert.GetTypeCode(five)
            };

            using (var stream = new MemoryStream())
            {
                transcoder.Encode(stream, five, flags, OpCode.Get);
                var actual = transcoder.Decode<int>(stream.ToArray(), flags, OpCode.Get);
                Assert.Equal(five, actual);
            }

        }

        [Fact]
        public void Test_Deserialize_Null()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter());
            object value = null;

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = TypeCode.Empty
            };

            // ReSharper disable once ExpressionIsAlwaysNull
            using (var stream = new MemoryStream())
            {
                transcoder.SerializeAsJson(stream, value);
                var actual = transcoder.Decode<object>(stream.ToArray(), flags, OpCode.Get);
                Assert.Equal(value, actual);
            }
        }

        [Fact]
        public void Test_Deserialize_String()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter());
            var value = "astring";

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = Convert.GetTypeCode(value)
            };

            using (var stream = new MemoryStream())
            {
                transcoder.Encode(stream, value, flags, OpCode.Get);
                var actual = transcoder.Decode<string>(stream.ToArray(), flags, OpCode.Get);
                Assert.Equal(value, actual);
            }
        }

        [Fact]
        public void Test_Deserialize_Char()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter());
            var value = 'o';

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Reserved,
                TypeCode = Convert.GetTypeCode(value)
            };

            var bytes = Encoding.UTF8.GetBytes(value.ToString());
            var actual = transcoder.Decode<char>(bytes.AsMemory(), flags, OpCode.Get);
            Assert.Equal(value, actual);
        }

        [Fact]
        public void Test_Byte_Array()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter());
            var value = new byte[] { 0x00, 0x00, 0x01 };

            var flags = new Flags
            {
                Compression = Compression.None,
                DataFormat = DataFormat.Binary,
                TypeCode = Type.GetTypeCode(typeof(byte[]))
            };

            using (var stream = new MemoryStream())
            {
                transcoder.Encode(stream, value, flags, OpCode.Get);
                Assert.Equal(value, stream.ToArray());

                var actual = transcoder.Decode<byte[]>(stream.ToArray(), flags, OpCode.Get);
                Assert.Equal(value, actual);
            }
        }

        public class Person
        {
            public string Name { get; set; }
        }

        [Fact]
        public void Test_Json_Deserialize_Int()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter());
            int value = 42;

            using (var stream = new MemoryStream())
            {
                transcoder.SerializeAsJson(stream, value);
                var actual = transcoder.DeserializeAsJson<int>(stream.ToArray());

                Assert.Equal(value, actual);
            }
        }

        [Fact]
        public void Should_Hydrate_Poco_In_PascalCase_Whatever_The_Case_In_Json()
        {
            byte[] jsonData = Encoding.UTF8.GetBytes("{ \"SomeProperty\": \"SOME\", \"someIntProperty\": 12345, \"haspAscalCASE\": true }");
            var transcoder = new DefaultTranscoder(new DefaultConverter());
            var hydrated = transcoder.DeserializeAsJson<Pascal>(jsonData.AsMemory());

            Assert.Equal("SOME", hydrated.SomeProperty);
            Assert.Equal(12345, hydrated.SomeIntProperty);
            Assert.True(hydrated.HasPascalCase);
        }

        [Fact]
        public void Should_Convert_To_CamelCase_Json_With_Default_Serialization_Settings()
        {
            var transcoder = new DefaultTranscoder(new DefaultConverter());
            var data = new Pascal
            {
                SomeProperty = "SOME",
                SomeIntProperty = 12345,
                HasPascalCase = true
            };
            var expectedJsonBytes = Encoding.UTF8.GetBytes("{\"someProperty\":\"SOME\",\"someIntProperty\":12345,\"hasPascalCase\":true}");

            using (var jsonBytes = new MemoryStream())
            using (var jsonEncoded = new MemoryStream())
            {
                transcoder.SerializeAsJson(jsonBytes, data);
                transcoder.Encode(jsonEncoded, data, new Flags {DataFormat = DataFormat.Json}, OpCode.Get);
                Assert.Equal(expectedJsonBytes, jsonBytes.ToArray());
                Assert.Equal(expectedJsonBytes, jsonEncoded.ToArray());
            }
        }

        [Fact]
        public void Should_Convert_To_PascalCase_Json_With_Altered_Serialization_Settings()
        {
            var transcoder = new DefaultTranscoder(
                new DefaultConverter(),
                new DefaultSerializer(
                    new JsonSerializerSettings(),
                    new JsonSerializerSettings
                    {
                        ContractResolver = new DefaultContractResolver()
                    }));

            var data = new Pascal
            {
                SomeProperty = "SOME",
                SomeIntProperty = 12345,
                HasPascalCase = true
            };
            var expectedJsonBytes = Encoding.UTF8.GetBytes("{\"SomeProperty\":\"SOME\",\"SomeIntProperty\":12345,\"HasPascalCase\":true}");

            using (var jsonBytes = new MemoryStream())
            using (var jsonEncoded = new MemoryStream())
            {
                transcoder.SerializeAsJson(jsonBytes, data);
                transcoder.Encode(jsonEncoded, data, TypeCode.Object, OpCode.Get);

                Assert.Equal(expectedJsonBytes, jsonBytes.ToArray());
                Assert.Equal(expectedJsonBytes, jsonEncoded.ToArray());
            }
        }

        [Fact]
        public void When_ByteArray_Is_Stored_With_Legacy_Flags_It_Is_Decoded_As_A_ByteArray()
        {
            var legacyByteArray = new byte[]
            {
                129, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 8, 0,
                0, 0, 5, 19, 185, 8, 248, 3, 104, 208, 188, 0,
                0, 250, 82, 116, 101, 115, 116
            };

            var converter = new DefaultConverter();
            var format = new byte();

            var temp = converter.ToByte(legacyByteArray.AsSpan(24));
            converter.SetBit(ref format, 0, converter.GetBit(temp, 0));
            converter.SetBit(ref format, 1, converter.GetBit(temp, 1));
            converter.SetBit(ref format, 2, converter.GetBit(temp, 2));
            converter.SetBit(ref format, 3, converter.GetBit(temp, 3));

            var compression = new byte();
            converter.SetBit(ref compression, 4, converter.GetBit(temp, 4));
            converter.SetBit(ref compression, 5, converter.GetBit(temp, 5));
            converter.SetBit(ref compression, 6, converter.GetBit(temp, 6));

            var flags = new Flags
            {
                DataFormat = (DataFormat)format,
                Compression = (Compression)compression,
                TypeCode = (TypeCode)(converter.ToUInt16(legacyByteArray.AsSpan(26)) & 0xff),
            };

            var transcoder = new DefaultTranscoder(new DefaultConverter());
            var result = transcoder.Decode<byte[]>(legacyByteArray.AsMemory(28, 4), flags, OpCode.Get);
            Assert.Equal("test", Encoding.UTF8.GetString(result));
        }
    }

    /// <summary>
    /// 'Pascal' POCO for testing PascalCase related conversions.
    /// </summary>
    class Pascal
    {
        public string SomeProperty { get; set; }

        public int SomeIntProperty { get; set; }

        public bool HasPascalCase { get; set; }
    }
}

#region [ License information          ]
/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
#endregion
