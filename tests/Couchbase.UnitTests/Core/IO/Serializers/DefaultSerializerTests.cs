using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Reflection;
using System.Text;
using Couchbase.Core.IO.Serializers;
using Couchbase.Test.Common.Utils;
using Couchbase.UnitTests.Core.IO.Transcoders;
using Couchbase.UnitTests.Fixtures;
using Couchbase.UnitTests.Utils;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Serializers
{
    public class DefaultSerializerTests : IClassFixture<JsonConvertFixture>
    {
        #region DeserializationOptions

        [Fact]
        public void DeserializationOptions_Modification_UpdatesEffectiveSettings()
        {
            // Arrange

            var options = new DeserializationOptions();
            var effectiveSettings = new JsonSerializerSettings();

            var serializer = new Mock<DefaultSerializer>
            {
                CallBase = true
            };

            serializer.Setup(p => p.GetDeserializationSettings(It.IsAny<JsonSerializerSettings>(), options))
                .Returns(effectiveSettings);

            // Act

            serializer.Object.DeserializationOptions = options;

            // Assert

            Assert.Equal(effectiveSettings, serializer.Object.EffectiveDeserializationSettings);
        }

        [Fact]
        public void JsonSettings_With_Null_ContractResolver_Defaults_To_DefaultContractResolver()
        {
            JsonConvert.DefaultSettings = null; // no default settings available

            // Arrange
            var deserializationSettings = new JsonSerializerSettings
            {
                ContractResolver = null
            };
            var serializationSettings = new JsonSerializerSettings
            {
                ContractResolver = null
            };

            // Act
            var serializer = new DefaultSerializer(deserializationSettings, serializationSettings);

            // Assert
            Assert.IsAssignableFrom<DefaultContractResolver>(serializer.DeserializationSettings.ContractResolver);
            Assert.IsAssignableFrom<DefaultContractResolver>(serializer.SerializerSettings.ContractResolver);
        }

        [Fact]
        public void JsonSettings_With_Null_ContractResolver_Uses_JsonConvert_Default_ContractResolver_If_Available()
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            var deserializationSettings = new JsonSerializerSettings
            {
                ContractResolver = null
            };
            var serializationSettings = new JsonSerializerSettings
            {
                ContractResolver = null
            };

            var serializer = new DefaultSerializer(deserializationSettings, serializationSettings);

            Assert.IsAssignableFrom<CamelCasePropertyNamesContractResolver>(serializer.DeserializationSettings.ContractResolver);
            Assert.IsAssignableFrom<CamelCasePropertyNamesContractResolver>(serializer.SerializerSettings.ContractResolver);
        }

        #endregion

        #region Deserialize

        [Fact]
        public void Deserialize_FromMemory_Repeated()
        {
            // Arrange

            var serializer = new DefaultSerializer();

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\emmy-lou.json")!;
            var document = new byte[stream.Length];
            _ = stream.Read(document, 0, document.Length);

            // Act

            var result = serializer.Deserialize<JsonTranscoderTests.Person>(document);
            var result2 = serializer.Deserialize<JsonTranscoderTests.Person>(document);
            var result3 = serializer.Deserialize<JsonTranscoderTests.Person>(document);

            // Assert

            Assert.NotNull(result);
            Assert.NotNull(result2);
            Assert.NotNull(result3);
        }

        [Fact]
        public void Deserialize_FromReadOnlySequence_Repeated()
        {
            // Arrange

            var serializer = new DefaultSerializer();

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\emmy-lou.json")!;
            var document = new byte[stream.Length];
            _ = stream.Read(document, 0, document.Length);

            var split = SequenceHelpers.CreateSequenceWithMaxSegmentSize(document, 32);

            // Act

            var result = serializer.Deserialize<JsonTranscoderTests.Person>(split);
            var result2 = serializer.Deserialize<JsonTranscoderTests.Person>(split);
            var result3 = serializer.Deserialize<JsonTranscoderTests.Person>(split);

            // Assert

            Assert.NotNull(result);
            Assert.NotNull(result2);
            Assert.NotNull(result3);
        }

        [Fact]
        public void Deserialize_BoundarySurrogatePair_Success()
        {
            // Arrange

            var sb = new StringBuilder(1100);
            sb.Append('a', 1019);

            // Hex code D83E must show up in the character position 1022 in the output buffer. (Output buffer length 1024)
            // This causes Utf8MemoryReader's _decoder.Convert to return a character read length of 1022 instead of 1023 since D83E is a high surrogate half.
            for (var i = 0; i < 5; i++)
            {
                sb.Append("\ud83e\udd3a");
            }

            var str = sb.ToString();
            var jsonStr = JsonConvert.SerializeObject(str);
            var bytes = new UTF8Encoding(false).GetBytes(jsonStr);

            // Act

            var result = DefaultSerializer.Instance.Deserialize<string>(bytes);

            // Assert

            Assert.Equal(str, result);
        }

        #endregion

        #region Deserialize With ICustomObjectCreator

        [Fact]
        public void Deserialize_Stream_WithICustomObjectCreator_CreatesCustomObjects()
        {
            // Arrange

            var creator = new FakeCustomObjectCreator();

            var settings = new JsonSerializerSettings()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            var serializer = new DefaultSerializer(settings, settings)
            {
                DeserializationOptions = new DeserializationOptions()
                {
                    CustomObjectCreator = creator
                }
            };

            var stream = new MemoryStream(Encoding.UTF8.GetBytes("{\"subNode\":{\"property\":\"value\"}}"));

            // Act

            var result = serializer.Deserialize<JsonDocument>(stream);

            // Assert

            Assert.NotNull(result);
            Assert.NotNull(result.SubNode);
            Assert.Equal(typeof(DocumentSubNodeInherited), result.SubNode.GetType());
            Assert.Equal("value", result.SubNode.Property);
        }

        [Fact]
        public void Deserialize_ByteArray_WithICustomObjectCreator_CreatesCustomObjects()
        {
            // Arrange

            var creator = new FakeCustomObjectCreator();

            var settings = new JsonSerializerSettings()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            var serializer = new DefaultSerializer(settings, settings)
            {
                DeserializationOptions = new DeserializationOptions()
                {
                    CustomObjectCreator = creator
                }
            };

            var jsonBuffer = Encoding.UTF8.GetBytes("{\"subNode\":{\"property\":\"value\"}}");

            // Act

            var result = serializer.Deserialize<JsonDocument>(jsonBuffer.AsMemory());

            // Assert

            Assert.NotNull(result);
            Assert.NotNull(result.SubNode);
            Assert.Equal(typeof(DocumentSubNodeInherited), result.SubNode.GetType());
            Assert.Equal("value", result.SubNode.Property);
        }

        #endregion

        #region Serialize

        [Fact]
        public void Serialize_BufferWriter_Valid()
        {
            // Arrange

            var serializer = new DefaultSerializer();

#if NET6_0_OR_GREATER
            var writer = new ArrayBufferWriter<byte>();
#else
            using var memoryStream = new MemoryStream();
            var writer = PipeWriter.Create(memoryStream);
#endif

            // Act

            serializer.Serialize(writer, PersonExample);

            // Assert

            // Deserializing again ensures that valid JSON was produced

#if NET6_0_OR_GREATER
            Assert.Equal(PersonExampleExpectedJson, Encoding.UTF8.GetString(writer.WrittenSpan));
#else
            writer.FlushAsync().GetAwaiter().GetResult(); // completes synchronously

            Assert.Equal(PersonExampleExpectedJson, Encoding.UTF8.GetString(memoryStream.ToArray()));
#endif
        }

#endregion

        #region GetMemberName

        [Fact]
        public void GetMemberName_Null_ArgumentNullException()
        {
            // Arrange

            var serializer = new DefaultSerializer();

            // Act/Assert

            Assert.Throws<ArgumentNullException>(() => serializer.GetMemberName(null));
        }

        [Fact]
        public void GetMemberName_BasicProperty_ReturnsPropertyName()
        {
            // Arrange

            var settings = new JsonSerializerSettings()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            var serializer = new DefaultSerializer(settings, settings);

            // Act

            var result = serializer.GetMemberName(typeof (JsonDocument).GetTypeInfo().GetProperty("BasicProperty"));

            // Assert

            Assert.Equal("basicProperty", result);
        }

        [Fact]
        public void GetMemberName_NamedProperty_ReturnsNameFromAttribute()
        {
            // Arrange

            var settings = new JsonSerializerSettings()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            var serializer = new DefaultSerializer(settings, settings);

            // Act

            var result = serializer.GetMemberName(typeof(JsonDocument).GetTypeInfo().GetProperty("NamedProperty"));

            // Assert

            Assert.Equal("useThisName", result);
        }

        [Fact]
        public void GetMemberName_IgnoredProperty_ReturnsNull()
        {
            // Arrange

            var settings = new JsonSerializerSettings()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            var serializer = new DefaultSerializer(settings, settings);

            // Act

            var result = serializer.GetMemberName(typeof(JsonDocument).GetTypeInfo().GetProperty("IgnoredProperty"));

            // Assert

            Assert.Null(result);
        }

        #endregion

        #region CreateStreamingDeserializer

        [Fact]
        public void CreateJsonStreamReader_UsesDeserializerSettings()
        {
            // Arrange

            var serializer = new DefaultSerializer(new JsonSerializerSettings
            {
                DateFormatHandling = DateFormatHandling.MicrosoftDateFormat
            }, new JsonSerializerSettings
            {
                DateFormatHandling = DateFormatHandling.IsoDateFormat
            });

            using var stream = new MemoryStream();

            // Act

            var result = (DefaultJsonStreamReader) serializer.CreateJsonStreamReader(stream);

            // Assert

            Assert.Equal(DateFormatHandling.MicrosoftDateFormat, result.Deserializer.DateFormatHandling);
        }

        #endregion

        #region Helpers

        private class JsonDocument
        {
            public string BasicProperty { get; set; }

            [JsonProperty("useThisName")]
            public string NamedProperty { get; set; }

            [JsonIgnore]
            public string IgnoredProperty { get; set; }

            public DocumentSubNode SubNode { get; set; }
        }

        private class DocumentSubNode
        {
            public string Property { get; set; }
        }

        private class DocumentSubNodeInherited : DocumentSubNode
        {
        }

        private class FakeCustomObjectCreator : ICustomObjectCreator
        {
            public bool CanCreateObject(Type type)
            {
                return type == typeof (DocumentSubNode);
            }

            public object CreateObject(Type type)
            {
                return new DocumentSubNodeInherited();
            }
        }

        public class Dimensions
        {
            public int Height { get; set; }
            public int Weight { get; set; }
        }

        public class Location
        {
            [JsonProperty("lat")]
            public double Latitude { get; set; }
            [JsonProperty("long")]
            public double Longitude { get; set; }
        }

        public class Details
        {
            public Location Location { get; set; }
        }

        public class Hobby
        {
            public string Type { get; set; }
            public string Name { get; set; }
            public Details Details { get; set; }
        }

        public class Attributes
        {
            [JsonProperty("hair")]
            public string HairColor { get; set; }
            public Dimensions Dimensions { get; set; }
            public List<Hobby> Hobbies { get; set; }
        }

        public class Person
        {
            public string Name { get; set; }
            public int Age { get; set; }
            public List<string> Animals { get; set; }
            public Attributes Attributes { get; set; }
        }

        public static readonly Person PersonExample = new()
        {
            Name = "Emmy-lou Dickerson",
            Age = 26,
            Animals = new() { "cat", "dog", "parrot" },
            Attributes = new()
            {
                HairColor = "brown"
            }
        };

        public const string PersonExampleExpectedJson =
            "{\"name\":\"Emmy-lou Dickerson\",\"age\":26,\"animals\":[\"cat\",\"dog\",\"parrot\"],\"attributes\":{\"hair\":\"brown\",\"dimensions\":null,\"hobbies\":null}}";


        #endregion
    }
}
