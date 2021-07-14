using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;
using Couchbase.Query;
using Couchbase.UnitTests.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Serializers
{
    public class DefaultJsonStreamReaderTests
    {
        #region InitializeAsync

        [Fact]
        public async Task InitializeAsync_CalledOnce_ReturnsTrue()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Views\200-success.json");

            using var reader = new DefaultJsonStreamReader(stream, CreateDefaultJsonSerializer());

            // Act

            var result = await reader.InitializeAsync();

            // Assert

            Assert.True(result);
        }

        [Fact]
        public async Task InitializeAsync_CalledTwice_InvalidOperationException()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Views\200-success.json");

            using var reader = new DefaultJsonStreamReader(stream, CreateDefaultJsonSerializer());

            // Act/Assert

            await reader.InitializeAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => reader.InitializeAsync());
        }

        [Fact]
        public async Task InitializeAsync_EmptyStream_ReturnsFalse()
        {
            // Arrange

            using var stream = new MemoryStream();

            using var reader = new DefaultJsonStreamReader(stream, CreateDefaultJsonSerializer());

            // Act

            var result = await reader.InitializeAsync();

            // Assert

            Assert.False(result);
        }

        #endregion

        #region ReadToNextAttributeAsync

        [Fact]
        public async Task ReadToNextAttributeAsync_FirstTime_ReturnsFirstAttribute()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Views\200-success.json");

            using var reader = new DefaultJsonStreamReader(stream, CreateDefaultJsonSerializer());

            Assert.True(await reader.InitializeAsync());

            // Act

            var result = await reader.ReadToNextAttributeAsync();

            // Assert

            Assert.Equal("total_rows", result);
        }

        [Fact]
        public async Task ReadToNextAttributeAsync_SecondTime_ReturnsFirstAttribute()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Views\200-success.json");

            using var reader = new DefaultJsonStreamReader(stream, CreateDefaultJsonSerializer());

            Assert.True(await reader.InitializeAsync());

            // Act

            await reader.ReadToNextAttributeAsync();
            var result = await reader.ReadToNextAttributeAsync();

            // Assert

            Assert.Equal("rows", result);
        }

        [Fact]
        public async Task ReadToNextAttributeAsync_AfterLast_ReturnsNull()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"404-view-notfound.json");

            using var reader = new DefaultJsonStreamReader(stream, CreateDefaultJsonSerializer());

            Assert.True(await reader.InitializeAsync());

            // Act

            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            var result = await reader.ReadToNextAttributeAsync();

            // Assert

            Assert.Null(result);
        }

        #endregion

        #region ReadArrayAsync

        [Fact]
        public async Task ReadArrayAsync_ReturnsArray()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Views\200-success.json");

            using var reader = new DefaultJsonStreamReader(stream, CreateDefaultJsonSerializer());

            Assert.True(await reader.InitializeAsync());

            // Act

            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            var result = await reader.ReadObjectsAsync<dynamic>().ToListAsync();

            // Assert

            Assert.Equal(4, result.Count);
            Assert.Equal("21st_amendment_brewery_cafe", (string) result[0]["id"]);
        }

        [Fact]
        public async Task ReadArrayAsync_AfterArray_GetsNextProperty()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-n1ql-error-response-400.json");

            using var reader = new DefaultJsonStreamReader(stream, CreateDefaultJsonSerializer());

            Assert.True(await reader.InitializeAsync());

            // Act

            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadObjectsAsync<dynamic>().ToListAsync();
            var result = await reader.ReadToNextAttributeAsync();

            // Assert

            Assert.Equal("status", result);
        }

        [Fact]
        public async Task ReadArrayAsync_NotOnArray_InvalidOperationException()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Views\200-success.json");

            using var reader = new DefaultJsonStreamReader(stream, CreateDefaultJsonSerializer());

            Assert.True(await reader.InitializeAsync());

            // Act/Assert

            await reader.ReadToNextAttributeAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => reader.ReadObjectsAsync<dynamic>().ToListAsync().AsTask());
        }

        #endregion

        #region ReadObjectAsync

        [Fact]
        public async Task ReadObjectAsync_ReturnsObject()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-n1ql-error-response-400.json");

            using var reader = new DefaultJsonStreamReader(stream, CreateDefaultJsonSerializer());

            Assert.True(await reader.InitializeAsync());

            // Act

            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadObjectsAsync<dynamic>().ToListAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            var result = await reader.ReadObjectAsync<MetricsData>();

            // Assert

            Assert.NotNull(result);
            Assert.Equal((uint) 1, result.ErrorCount);
        }

        [Fact]
        public async Task ReadObjectAsync_AfterObject_GetsNextProperty()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-n1ql-error-response-400.json");

            using var reader = new DefaultJsonStreamReader(stream, CreateDefaultJsonSerializer());

            Assert.True(await reader.InitializeAsync());

            // Act

            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadObjectsAsync<dynamic>().ToListAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadObjectAsync<MetricsData>();
            var result = await reader.ReadToNextAttributeAsync();

            // Assert

            Assert.Null(result);
        }

        [Fact]
        public async Task ReadObjectAsync_OnString_ReadsString()
        {
            // Arrange

            using var stream = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes("{\"value\":\"string\"}"));

            using var reader = new DefaultJsonStreamReader(stream, CreateDefaultJsonSerializer());

            Assert.True(await reader.InitializeAsync());

            // Act/Assert

            await reader.ReadToNextAttributeAsync();
            var value = await reader.ReadObjectAsync<string>();
            Assert.Equal("string", value);
        }

        [Fact]
        public async Task ReadObjectAsync_OnNumber_ReadsNumber()
        {
            // Arrange

            using var stream = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes("{\"value\":0.105}"));

            using var reader = new DefaultJsonStreamReader(stream, CreateDefaultJsonSerializer());

            Assert.True(await reader.InitializeAsync());

            // Act/Assert

            await reader.ReadToNextAttributeAsync();
            var value = await reader.ReadObjectAsync<double>();
            Assert.Equal(0.105, value);
        }

        [Fact]
        public async Task ReadObjectAsync_OnDateTimeOffset_PreservesTimeZone()
        {
            {
                using var stream = new MemoryStream(
                    System.Text.Encoding.UTF8.GetBytes("{\"value\":\"2021-06-21T10:16:56.9714243+10:00\"}"));

                var serializer = CreateDefaultJsonSerializer();
                serializer.DateParseHandling = DateParseHandling.DateTimeOffset;
                using var reader = new DefaultJsonStreamReader(stream, serializer);
                Assert.True(await reader.InitializeAsync());


                await reader.ReadToNextAttributeAsync();
                var value = await reader.ReadObjectAsync<DateTimeOffset>();
                Assert.Equal(10, value.Offset.Hours);
            }
            {
                // check again, just in case the test was run in the +10 timezone and would have given a false positive.
                using var stream = new MemoryStream(
                    System.Text.Encoding.UTF8.GetBytes("{\"value\":\"2021-06-21T10:16:56.9714243-8:00\"}"));

                var serializer = CreateDefaultJsonSerializer();
                serializer.DateParseHandling = DateParseHandling.DateTimeOffset;
                using var reader = new DefaultJsonStreamReader(stream, serializer);
                Assert.True(await reader.InitializeAsync());


                await reader.ReadToNextAttributeAsync();
                var value = await reader.ReadObjectAsync<DateTimeOffset>();
                Assert.Equal(-8, value.Offset.Hours);
            }
        }

        #endregion

        #region Value

        [Theory]
        [InlineData(@"Documents\Views\200-success.json", 4L)]
        [InlineData(@"Documents\Query\query-n1ql-error-response-400.json", "922edd9a-23d7-4053-8d60-91f7cbc22c83")]
        public async Task Value_OnProperty_ReturnsValue(string filename, object expectedFirstProperty)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(filename);

            using var reader = new DefaultJsonStreamReader(stream, CreateDefaultJsonSerializer());

            Assert.True(await reader.InitializeAsync());

            // Act

            await reader.ReadToNextAttributeAsync();
            var result = reader.Value;

            // Assert

            Assert.Equal(expectedFirstProperty, result);
        }

        [Fact]
        public async Task Value_NotOnProperty_ReturnsNull()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Views\200-success.json");

            using var reader = new DefaultJsonStreamReader(stream, CreateDefaultJsonSerializer());

            Assert.True(await reader.InitializeAsync());

            // Act

            var result = reader.Value;

            // Assert

            Assert.Null(result);
        }

        #endregion

        #region ValueType

        [Theory]
        [InlineData(@"Documents\Views\200-success.json", typeof(long))]
        [InlineData(@"Documents\Query\query-n1ql-error-response-400.json", typeof(string))]
        public async Task ValueType_OnProperty_ReturnsValue(string filename, Type expectedFirstProperty)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(filename);

            using var reader = new DefaultJsonStreamReader(stream, CreateDefaultJsonSerializer());

            Assert.True(await reader.InitializeAsync());

            // Act

            await reader.ReadToNextAttributeAsync();
            var result = reader.ValueType;

            // Assert

            Assert.Equal(expectedFirstProperty, result);
        }

        [Fact]
        public async Task ValueType_NotOnProperty_ReturnsNull()
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Views\200-success.json");

            using var reader = new DefaultJsonStreamReader(stream, CreateDefaultJsonSerializer());

            Assert.True(await reader.InitializeAsync());

            // Act

            var result = reader.ValueType;

            // Assert

            Assert.Null(result);
        }

        #endregion

        #region Helpers

        private static JsonSerializer CreateDefaultJsonSerializer() =>
            JsonSerializer.Create(new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });

        #endregion
    }
}
