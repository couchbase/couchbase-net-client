using System.Collections.Generic;
using System.Text;
using Couchbase.Core.IO.Serializers;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Serializers
{
    public class NewtonsoftProjectionBuilderTests
    {
        // AddChildren is used when the projection exceeds the subdoc spec limit and the SDK falls back
        // to fetching the full document and projecting client-side.

        [Fact]
        public void AddChildren_NestedPath_ReconstructsNesting()
        {
            // Arrange
            var fullDoc = Encoding.UTF8.GetBytes("{\"a\":{\"name\":\"bar\"},\"ignored\":5}");
            var builder = CreateBuilder();

            // Act - request a nested, dot-separated path
            builder.AddChildren(new[] { "a.name" }, fullDoc);
            var result = builder.ToObject<Dictionary<string, JToken>>();

            // Assert - reconstructed as { "a": { "name": "bar" } }, "ignored" omitted
            Assert.True(result.ContainsKey("a"));
            Assert.Equal("bar", (string)result["a"]["name"]);
            Assert.False(result.ContainsKey("ignored"));
        }

        [Fact]
        public void AddChildren_TopLevelPath_Adds()
        {
            var fullDoc = Encoding.UTF8.GetBytes("{\"a\":{\"name\":\"bar\"},\"ignored\":5}");
            var builder = CreateBuilder();

            builder.AddChildren(new[] { "a" }, fullDoc);
            var result = builder.ToObject<Dictionary<string, JToken>>();

            Assert.True(result.ContainsKey("a"));
            Assert.Equal("bar", (string)result["a"]["name"]);
            Assert.False(result.ContainsKey("ignored"));
        }

        [Fact]
        public void AddChildren_NonExistentPath_IsOmitted()
        {
            var fullDoc = Encoding.UTF8.GetBytes("{\"a\":{\"name\":\"bar\"}}");
            var builder = CreateBuilder();

            builder.AddChildren(new[] { "does.not.exist" }, fullDoc);
            var result = builder.ToObject<Dictionary<string, JToken>>();

            Assert.Empty(result);
        }

        private static IProjectionBuilder CreateBuilder() =>
            DefaultSerializer.Instance.CreateProjectionBuilder(new Mock<ILogger>().Object);
    }
}
