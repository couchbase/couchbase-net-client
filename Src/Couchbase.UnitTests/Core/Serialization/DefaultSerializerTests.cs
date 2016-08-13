using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Serialization;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace Couchbase.UnitTests.Core.Serialization
{
    [TestFixture]
    public class DefaultSerializerTests
    {
        private Func<JsonSerializerSettings> _savedSerializerSettings;
        public void OneTimeSetUp()
        {
            _savedSerializerSettings = JsonConvert.DefaultSettings;
        }

        #region DeserializationOptions

        [Test]
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

            Assert.AreEqual(effectiveSettings, serializer.Object.EffectiveDeserializationSettings);
        }

        [Test]
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
            Assert.IsInstanceOf<DefaultContractResolver>(serializer.DeserializationSettings.ContractResolver);
            Assert.IsInstanceOf<DefaultContractResolver>(serializer.SerializerSettings.ContractResolver);
        }

        [Test]
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

            Assert.IsInstanceOf<CamelCasePropertyNamesContractResolver>(serializer.DeserializationSettings.ContractResolver);
            Assert.IsInstanceOf<CamelCasePropertyNamesContractResolver>(serializer.SerializerSettings.ContractResolver);
        }

        #endregion

        #region Deserialize With ICustomObjectCreator

        [Test]
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
            Assert.AreEqual(typeof(DocumentSubNodeInherited), result.SubNode.GetType());
            Assert.AreEqual("value", result.SubNode.Property);
        }

        [Test]
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

            var result = serializer.Deserialize<JsonDocument>(jsonBuffer, 0, jsonBuffer.Length);

            // Assert

            Assert.NotNull(result);
            Assert.NotNull(result.SubNode);
            Assert.AreEqual(typeof(DocumentSubNodeInherited), result.SubNode.GetType());
            Assert.AreEqual("value", result.SubNode.Property);
        }

        #endregion

        #region GetMemberName

        [Test]
        public void GetMemberName_Null_ArgumentNullException()
        {
            // Arrange

            var serializer = new DefaultSerializer();

            // Act/Assert

            Assert.Throws<ArgumentNullException>(() => serializer.GetMemberName(null));
        }

        [Test]
        public void GetMemberName_BasicProperty_ReturnsPropertyName()
        {
            // Arrange

            var settings = new JsonSerializerSettings()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            var serializer = new DefaultSerializer(settings, settings);

            // Act

            var result = serializer.GetMemberName(typeof (JsonDocument).GetProperty("BasicProperty"));

            // Assert

            Assert.AreEqual("basicProperty", result);
        }

        [Test]
        public void GetMemberName_NamedProperty_ReturnsNameFromAttribute()
        {
            // Arrange

            var settings = new JsonSerializerSettings()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            var serializer = new DefaultSerializer(settings, settings);

            // Act

            var result = serializer.GetMemberName(typeof(JsonDocument).GetProperty("NamedProperty"));

            // Assert

            Assert.AreEqual("useThisName", result);
        }

        [Test]
        public void GetMemberName_IgnoredProperty_ReturnsNull()
        {
            // Arrange

            var settings = new JsonSerializerSettings()
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            var serializer = new DefaultSerializer(settings, settings);

            // Act

            var result = serializer.GetMemberName(typeof(JsonDocument).GetProperty("IgnoredProperty"));

            // Assert

            Assert.IsNull(result);
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

        [OneTimeTearDown]
        public void OneTimeTearDown_CleanUpJsonConvert()
        {
            JsonConvert.DefaultSettings = _savedSerializerSettings;
        }
    }
}
