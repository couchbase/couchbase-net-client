using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;
using Couchbase.Test.Common.Utils;
using Couchbase.UnitTests.Utils;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Serializers.SystemTextJson
{
    public class SystemTextJsonSerializerTests
    {
        #region Deserialize

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Deserialize_FromStream_Success(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\emmy-lou.json");

            var serializer = CreateSerializer(withContext);

            // Act

            var result = serializer.Deserialize<Person>(stream);

            // Assert

            Assert.NotNull(result);
            Assert.Equal("Emmy-lou Dickerson", result.Name);
            Assert.Equal(49.282730, result.Attributes.Hobbies[1].Details.Location.Latitude);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Deserialize_FromEmptyStream_Null(bool withContext)
        {
            // Arrange

            using var stream = new MemoryStream(0);

            var serializer = CreateSerializer(withContext);

            // Act

            var result = serializer.Deserialize<Person>(stream);

            // Assert

            Assert.Null(result);
        }

        [Fact(Skip = "Skipping until CI agents have the .NET 6 SDK and support source generation")]
        public void Deserialize_FromStreamTypeNotInContext_InvalidOperationException()
        {
            // Arrange

            using var stream = new MemoryStream(new byte[] { 0x7b, 0x7d }); // empty {}

            var serializer = CreateSerializer(true);

            // Act/Assert

            Assert.Throws<InvalidOperationException>(() => serializer.Deserialize<JsonDocument>(stream));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Deserialize_FromMemory_Success(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\emmy-lou.json");
            using var memoryStream = new MemoryStream((int) stream.Length);
            stream.CopyTo(memoryStream);
            memoryStream.Position = 0;

            var serializer = CreateSerializer(withContext);

            // Act

            var result = serializer.Deserialize<Person>(memoryStream.ToArray().AsMemory());

            // Assert

            Assert.NotNull(result);
            Assert.Equal("Emmy-lou Dickerson", result.Name);
            Assert.Equal(49.282730, result.Attributes.Hobbies[1].Details.Location.Latitude);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Deserialize_FromReadOnlySequence_Success(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\emmy-lou.json");
            using var memoryStream = new MemoryStream((int) stream.Length);
            stream.CopyTo(memoryStream);

            var sequence = SequenceHelpers.CreateSequenceWithMaxSegmentSize(memoryStream.ToArray(), 32);

            var serializer = CreateSerializer(withContext);

            // Act

            var result = serializer.Deserialize<Person>(sequence);

            // Assert

            Assert.NotNull(result);
            Assert.Equal("Emmy-lou Dickerson", result.Name);
            Assert.Equal(49.282730, result.Attributes.Hobbies[1].Details.Location.Latitude);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Deserialize_FromEmptyMemory_Null(bool withContext)
        {
            // Arrange

            ReadOnlyMemory<byte> memory = default;

            var serializer = CreateSerializer(withContext);

            // Act

            var result = serializer.Deserialize<Person>(memory);

            // Assert

            Assert.Null(result);
        }

        [Fact(Skip = "Skipping until CI agents have the .NET 6 SDK and support source generation")]
        public void Deserialize_FromMemoryTypeNotInContext_InvalidOperationException()
        {
            // Arrange

            ReadOnlyMemory<byte> memory = new byte[] { 0x7b, 0x7d }; // empty {}

            var serializer = CreateSerializer(true);

            // Act/Assert

            Assert.Throws<InvalidOperationException>(() => serializer.Deserialize<JsonDocument>(memory));
        }

        #endregion

        #region DeserializeAsync

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DeserializeAsync_FromStream_Success(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\emmy-lou.json");

            var serializer = CreateSerializer(withContext);

            // Act

            var result = await serializer.DeserializeAsync<Person>(stream);

            // Assert

            Assert.NotNull(result);
            Assert.Equal("Emmy-lou Dickerson", result.Name);
            Assert.Equal(49.282730, result.Attributes.Hobbies[1].Details.Location.Latitude);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DeserializeAsync_FromEmptyStream_Null(bool withContext)
        {
            // Arrange

            using var stream = new MemoryStream(0);

            var serializer = CreateSerializer(withContext);

            // Act

            var result = await serializer.DeserializeAsync<Person>(stream);

            // Assert

            Assert.Null(result);
        }

        [Fact(Skip = "Skipping until CI agents have the .NET 6 SDK and support source generation")]
        public async Task DeserializeAsync_FromStreamTypeNotInContext_InvalidOperationException()
        {
            // Arrange

            using var stream = new MemoryStream(new byte[] { 0x7b, 0x7d }); // empty {}

            var serializer = CreateSerializer(true);

            // Act/Assert

            await Assert.ThrowsAsync<InvalidOperationException>(() => serializer.DeserializeAsync<JsonDocument>(stream).AsTask());
        }

        #endregion

        #region Serialize

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Serialize_Untyped_Success(bool withContext)
        {
            // Arrange

            using var stream = new MemoryStream();

            var serializer = CreateSerializer(withContext);

            // Act

            serializer.Serialize(stream, (object)PersonExample);

            // Assert

            Assert.Equal(PersonExampleExpectedJson, Encoding.UTF8.GetString(stream.ToArray()));
        }

        [Fact(Skip = "Skipping until CI agents have the .NET 6 SDK and support source generation")]
        public void Serialize_UntypedNotInContext_InvalidOperationException()
        {
            // Arrange

            using var stream = new MemoryStream();

            var serializer = CreateSerializer(true);

            // Act/Assert

            Assert.Throws<InvalidOperationException>(() => serializer.Serialize(stream, (object)new JsonDocument()));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Serialize_Typed_Success(bool withContext)
        {
            // Arrange

            using var stream = new MemoryStream();

            var serializer = CreateSerializer(withContext);

            // Act

            serializer.Serialize(stream, PersonExample);

            // Assert

            Assert.Equal(PersonExampleExpectedJson, Encoding.UTF8.GetString(stream.ToArray()));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Serialize_BufferWriter_Success(bool withContext)
        {
            // Arrange

            var serializer = CreateSerializer(withContext);

#if NET6_0_OR_GREATER
            var writer = new ArrayBufferWriter<byte>();
#else
            using var memoryStream = new MemoryStream();
            var writer = PipeWriter.Create(memoryStream);
#endif

            // Act

            serializer.Serialize(writer, PersonExample);

            // Assert

#if NET6_0_OR_GREATER
            Assert.Equal(PersonExampleExpectedJson, Encoding.UTF8.GetString(writer.WrittenSpan));
#else
            writer.FlushAsync().GetAwaiter().GetResult(); // completes synchronously

            Assert.Equal(PersonExampleExpectedJson, Encoding.UTF8.GetString(memoryStream.ToArray()));
#endif
        }

        [Fact(Skip = "Skipping until CI agents have the .NET 6 SDK and support source generation")]
        public void Serialize_TypedNotInContext_InvalidOperationException()
        {
            // Arrange

            using var stream = new MemoryStream();

            var serializer = CreateSerializer(true);

            // Act/Assert

            Assert.Throws<InvalidOperationException>(() => serializer.Serialize(stream, new JsonDocument()));
        }

#endregion

        #region SerializeAsync

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SerializeAsync_Untyped_Success(bool withContext)
        {
            // Arrange

            using var stream = new MemoryStream();

            var serializer = CreateSerializer(withContext);

            // Act

            await serializer.SerializeAsync(stream, (object)PersonExample);

            // Assert

            Assert.Equal(PersonExampleExpectedJson, Encoding.UTF8.GetString(stream.ToArray()));
        }

        [Fact(Skip = "Skipping until CI agents have the .NET 6 SDK and support source generation")]
        public async Task SerializeAsync_UntypedNotInContext_InvalidOperationException()
        {
            // Arrange

            using var stream = new MemoryStream();

            var serializer = CreateSerializer(true);

            // Act/Assert

            await Assert.ThrowsAsync<InvalidOperationException>(() => serializer.SerializeAsync(stream, (object)new JsonDocument()).AsTask());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task SerializeAsync_Typed_Success(bool withContext)
        {
            // Arrange

            using var stream = new MemoryStream();

            var serializer = CreateSerializer(withContext);

            // Act

            await serializer.SerializeAsync(stream, PersonExample);

            // Assert

            Assert.Equal(PersonExampleExpectedJson, Encoding.UTF8.GetString(stream.ToArray()));
        }

        [Fact(Skip = "Skipping until CI agents have the .NET 6 SDK and support source generation")]
        public async Task SerializeAsync_TypedNotInContext_InvalidOperationException()
        {
            // Arrange

            using var stream = new MemoryStream();

            var serializer = CreateSerializer(true);

            // Act/Assert

            await Assert.ThrowsAsync<InvalidOperationException>(() => serializer.SerializeAsync(stream, new JsonDocument()).AsTask());
        }

        #endregion

        #region GetMemberName

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GetMemberName_Null_ArgumentNullException(bool withContext)
        {
            // Arrange

            var serializer = CreateSerializer(withContext);

            // Act/Assert

            Assert.Throws<ArgumentNullException>(() => serializer.GetMemberName(null!));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GetMemberName_BasicProperty_ReturnsPropertyName(bool withContext)
        {
            // Arrange

            var serializer = CreateSerializer(withContext);

            // Act

            var result = serializer.GetMemberName(typeof (JsonDocument).GetProperty(nameof(JsonDocument.BasicProperty))!);

            // Assert

            Assert.Equal("basicProperty", result);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GetMemberName_NamedProperty_ReturnsNameFromAttribute(bool withContext)
        {
            // Arrange

            var serializer = CreateSerializer(withContext);

            // Act

            var result = serializer.GetMemberName(typeof(JsonDocument).GetProperty(nameof(JsonDocument.NamedProperty))!);

            // Assert

            Assert.Equal("useThisName", result);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GetMemberName_IgnoredProperty_ReturnsNull(bool withContext)
        {
            // Arrange

            var serializer = CreateSerializer(withContext);

            // Act

            var result = serializer.GetMemberName(typeof(JsonDocument).GetProperty(nameof(JsonDocument.IgnoredProperty))!);

            // Assert

            Assert.Null(result);
        }

        #endregion

        #region Helpers

        private static SystemTextJsonSerializer CreateSerializer(bool withContext) =>
            withContext
                ? SystemTextJsonSerializer.Create(PersonContext.Default)
                : SystemTextJsonSerializer.Create();

        private class JsonDocument
        {
            public string BasicProperty { get; set; }

            [JsonPropertyName("useThisName")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string NamedProperty { get; set; }

            [JsonIgnore]
            public string IgnoredProperty { get; set; }
        }

        public class Dimensions
        {
            public int Height { get; set; }
            public int Weight { get; set; }
        }

        public class Location
        {
            [JsonPropertyName("lat")]
            public double Latitude { get; set; }
            [JsonPropertyName("long")]
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
            [JsonPropertyName("hair")]
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

        private static readonly Person PersonExample = new()
        {
            Name = "Emmy-lou Dickerson",
            Age = 26,
            Animals = new() { "cat", "dog", "parrot" },
            Attributes = new()
            {
                HairColor = "brown"
            }
        };

        private const string PersonExampleExpectedJson =
            "{\"name\":\"Emmy-lou Dickerson\",\"age\":26,\"animals\":[\"cat\",\"dog\",\"parrot\"],\"attributes\":{\"hair\":\"brown\",\"dimensions\":null,\"hobbies\":null}}";

        #endregion
    }
}
