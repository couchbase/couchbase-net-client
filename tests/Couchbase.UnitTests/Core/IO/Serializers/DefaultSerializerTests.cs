using System;
using System.IO;
using System.Reflection;
using System.Text;
using Couchbase.Core.IO.Serializers;
using Couchbase.UnitTests.Fixtures;
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

        #endregion
    }
}
