using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Couchbase.Core.IO.Serializers.SystemTextJson;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Serializers.SystemTextJson
{
    public class SystemTextJsonProjectionBuilderTests
    {
        #region AddPath

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AddPath_RootProperty_Adds(bool withContext)
        {
            // Arrange

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);
            JsonSerializer.Serialize(writer, new SystemTextJsonSerializerTests.Person { Name = "bar"}, Options);

            var builder = CreateProjectionBuilder(withContext);

            // Act

            builder.AddPath("a", stream.GetBuffer().AsMemory(0, (int) stream.Length));
            var result = builder.ToObject<ProjectionWrapper>();

            // Assert

            Assert.NotNull(result);
            Assert.NotNull(result.A);
            Assert.Equal("bar", result.A.Name);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AddPath_RootProperty_AddsRepeatedly(bool withContext)
        {
            // Arrange

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);
            JsonSerializer.Serialize(writer, new SystemTextJsonSerializerTests.Person { Name = "bar"}, Options);

            var builder = CreateProjectionBuilder(withContext);

            // Act

            builder.AddPath("a", stream.GetBuffer().AsMemory(0, (int) stream.Length));
            builder.AddPath("c", stream.GetBuffer().AsMemory(0, (int) stream.Length));
            var result = builder.ToObject<ProjectionWrapper>();

            // Assert

            Assert.NotNull(result);
            Assert.NotNull(result.A);
            Assert.NotNull(result.C);
            Assert.Equal("bar", result.A.Name);
            Assert.Equal("bar", result.C.Name);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AddPath_NestedProperty_Adds(bool withContext)
        {
            // Arrange

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);
            JsonSerializer.Serialize(writer, new SystemTextJsonSerializerTests.Attributes { HairColor = "bar"}, Options);

            var builder = CreateProjectionBuilder(withContext);

            // Act

            builder.AddPath("a.attributes", stream.GetBuffer().AsMemory(0, (int) stream.Length));
            var result = builder.ToObject<ProjectionWrapper>();

            // Assert

            Assert.NotNull(result);
            Assert.NotNull(result.A);
            Assert.NotNull(result.A.Attributes);
            Assert.Equal("bar", result.A.Attributes.HairColor);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AddPath_Mixed_Adds(bool withContext)
        {
            // Arrange

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);
            JsonSerializer.Serialize(writer, "foo", Options);

            using var stream2 = new MemoryStream();
            using var writer2 = new Utf8JsonWriter(stream2);
            JsonSerializer.Serialize(writer2, "bar", Options);

            var builder = CreateProjectionBuilder(withContext);

            // Act

            builder.AddPath("a.name", stream.GetBuffer().AsMemory(0, (int) stream.Length));
            builder.AddPath("a.attributes.hair", stream2.GetBuffer().AsMemory(0, (int) stream2.Length));
            var result = builder.ToObject<ProjectionWrapper>();

            // Assert

            Assert.NotNull(result);
            Assert.NotNull(result.A);
            Assert.NotNull(result.A.Attributes);
            Assert.Equal("foo", result.A.Name);
            Assert.Equal("bar", result.A.Attributes.HairColor);
        }

        #endregion

        #region AddChildren

        // AddChildren is used when the projection exceeds the subdoc spec limit and the SDK falls back
        // to fetching the full document and projecting client-side.

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AddChildren_NestedPath_ReconstructsNesting(bool withContext)
        {
            // Arrange
            var fullDoc = Encoding.UTF8.GetBytes("{\"a\":{\"name\":\"bar\"},\"ignored\":5}");
            var builder = CreateProjectionBuilder(withContext);

            // Act - request a nested, dot-separated path
            builder.AddChildren(new[] { "a.name" }, fullDoc);
            var result = builder.ToObject<ProjectionWrapper>();

            // Assert - reconstructed as { "a": { "name": "bar" } }
            Assert.NotNull(result);
            Assert.NotNull(result.A);
            Assert.Equal("bar", result.A.Name);
            Assert.Null(result.B);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AddChildren_TopLevelPath_Adds(bool withContext)
        {
            var fullDoc = Encoding.UTF8.GetBytes("{\"a\":{\"name\":\"bar\"},\"ignored\":5}");
            var builder = CreateProjectionBuilder(withContext);

            builder.AddChildren(new[] { "a" }, fullDoc);
            var result = builder.ToObject<ProjectionWrapper>();

            Assert.NotNull(result);
            Assert.NotNull(result.A);
            Assert.Equal("bar", result.A.Name);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void AddChildren_NonExistentPath_IsOmitted(bool withContext)
        {
            var fullDoc = Encoding.UTF8.GetBytes("{\"a\":{\"name\":\"bar\"}}");
            var builder = CreateProjectionBuilder(withContext);

            builder.AddChildren(new[] { "does.not.exist" }, fullDoc);
            var result = builder.ToObject<ProjectionWrapper>();

            Assert.NotNull(result);
            Assert.Null(result.A);
            Assert.Null(result.B);
            Assert.Null(result.C);
        }

        #endregion

        #region Helpers

        private static JsonSerializerOptions Options { get; } = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // TODO: Use context for tests once CI agents have the .NET 6 SDK and support source generation
        private static SystemTextJsonProjectionBuilder CreateProjectionBuilder(bool withContext)
        {
            JsonSerializerOptions options;

            if (withContext)
            {
                options = PersonContext.Default.Options;
            }
            else
            {
                options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                options.MakeReadOnly(populateMissingResolver: true);
            }

            return new SystemTextJsonProjectionBuilder(options);
        }

        public class ProjectionWrapper
        {
            public SystemTextJsonSerializerTests.Person A { get; set; }
            public SystemTextJsonSerializerTests.Person B { get; set; }
            public SystemTextJsonSerializerTests.Person C { get; set; }
        }

        #endregion
    }
}
