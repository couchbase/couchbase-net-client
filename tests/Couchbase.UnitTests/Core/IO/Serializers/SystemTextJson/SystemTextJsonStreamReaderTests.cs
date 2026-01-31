using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Serializers.SystemTextJson;
using Couchbase.Query;
using Couchbase.UnitTests.Utils;
using Couchbase.Views;
using Xunit;

namespace Couchbase.UnitTests.Core.IO.Serializers.SystemTextJson
{
    public class SystemTextJsonStreamReaderTests
    {
        #region InitializeAsync

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task InitializeAsync_CalledOnce_ReturnsTrue(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Views\200-success.json");

            using var reader = CreateStreamReader(stream, withContext);

            // Act

            var result = await reader.InitializeAsync();

            // Assert

            Assert.True(result);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task InitializeAsync_CalledTwice_InvalidOperationException(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Views\200-success.json");

            using var reader = CreateStreamReader(stream, withContext);

            // Act/Assert

            await reader.InitializeAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => reader.InitializeAsync());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task InitializeAsync_EmptyStream_ReturnsFalse(bool withContext)
        {
            // Arrange

            using var stream = new MemoryStream();

            using var reader = CreateStreamReader(stream, withContext);

            // Act

            var result = await reader.InitializeAsync();

            // Assert

            Assert.False(result);
        }

        #endregion

        #region ReadToNextAttributeAsync

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadToNextAttributeAsync_FirstTime_ReturnsFirstAttribute(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Views\200-success.json");

            using var reader = CreateStreamReader(stream, withContext);

            Assert.True(await reader.InitializeAsync());

            // Act

            var result = await reader.ReadToNextAttributeAsync();

            // Assert

            Assert.Equal("total_rows", result);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadToNextAttributeAsync_SecondTime_ReturnsFirstAttribute(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Views\200-success.json");

            using var reader = CreateStreamReader(stream, withContext);

            Assert.True(await reader.InitializeAsync());

            // Act

            await reader.ReadToNextAttributeAsync();
            var result = await reader.ReadToNextAttributeAsync();

            // Assert

            Assert.Equal("rows", result);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadToNextAttributeAsync_NestedProperties_GetsPathAndDepth(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-success.json");

            using var reader = CreateStreamReader(stream, withContext);

            Assert.True(await reader.InitializeAsync());
            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            Assert.Equal("signature", await reader.ReadToNextAttributeAsync());

            // Act

            var result = await reader.ReadToNextAttributeAsync();

            // Assert

            Assert.Equal("signature.*", result);
            Assert.Equal(2, reader.Depth);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadToNextAttributeAsync_NestedProperties_ReadsPast(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-success.json");

            using var reader = CreateStreamReader(stream, withContext);

            Assert.True(await reader.InitializeAsync());
            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            Assert.Equal("signature.*", await reader.ReadToNextAttributeAsync());

            // Act

            var result = await reader.ReadToNextAttributeAsync();

            // Assert

            Assert.Equal("results", result);
            Assert.Equal(1, reader.Depth);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadToNextAttributeAsync_NestedArray_ReadsInto(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-success.json");

            using var reader = CreateStreamReader(stream, withContext);

            Assert.True(await reader.InitializeAsync());
            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            Assert.Equal("results", await reader.ReadToNextAttributeAsync());

            // Act

            var result = await reader.ReadToNextAttributeAsync();

            // Assert

            Assert.Equal("results[0].abv", result);
            Assert.Equal(3, reader.Depth);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadToNextAttributeAsync_NestedArray_CorrectPathDuringIteration(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-200-success.json");

            using var reader = CreateStreamReader(stream, withContext);

            Assert.True(await reader.InitializeAsync());
            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            Assert.Equal("results", await reader.ReadToNextAttributeAsync());

            while (await reader.ReadToNextAttributeAsync() != "results[0].updatedUnixMillis")
            {
                // Loop to get to the last attr in the first element
            }

            // Act

            var result = await reader.ReadToNextAttributeAsync();

            // Assert

            Assert.Equal("results[1].abv", result);
            Assert.Equal(3, reader.Depth);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadToNextAttributeAsync_AfterLast_ReturnsNull(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"404-view-notfound.json");

            using var reader = CreateStreamReader(stream, withContext);

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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadArrayAsync_ReturnsArray(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Views\200-success.json");

            using var reader = CreateStreamReader(stream, withContext);

            Assert.True(await reader.InitializeAsync());

            // Act

            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
#pragma warning disable CS0618 // Type or member is obsolete
            var result = await reader.ReadObjectsAsync<ViewRow<string[],object>>().ToListAsync();
#pragma warning restore CS0618 // Type or member is obsolete

            // Assert

            Assert.Equal(4, result.Count);
            Assert.Equal("21st_amendment_brewery_cafe", (string) result[0].Id);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadArrayAsync_AfterArray_GetsNextProperty(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-n1ql-error-response-400.json");

            using var reader = CreateStreamReader(stream, withContext);

            Assert.True(await reader.InitializeAsync());

            // Act

            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadObjectsAsync<Error>().ToListAsync();
            var result = await reader.ReadToNextAttributeAsync();

            // Assert

            Assert.Equal("status", result);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadArrayAsync_NotOnArray_InvalidOperationException(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Views\200-success.json");

            using var reader = CreateStreamReader(stream, withContext);

            Assert.True(await reader.InitializeAsync());

            // Act/Assert

            await reader.ReadToNextAttributeAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(
#pragma warning disable CS0618 // Type or member is obsolete
                () => reader.ReadObjectsAsync<ViewRow<string[], object>>().ToListAsync().AsTask());
#pragma warning restore CS0618 // Type or member is obsolete
        }

        #endregion

        #region ReadObjectAsync

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadObjectAsync_ReturnsObject(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-n1ql-error-response-400.json");

            using var reader = CreateStreamReader(stream, withContext);

            Assert.True(await reader.InitializeAsync());

            // Act

            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadObjectsAsync<Error>().ToListAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            var result = await reader.ReadObjectAsync<MetricsData>();

            // Assert

            Assert.NotNull(result);
            Assert.Equal((uint) 1, result.ErrorCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadObjectAsync_AfterObject_GetsNextProperty(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-n1ql-error-response-400.json");

            using var reader = CreateStreamReader(stream, withContext);

            Assert.True(await reader.InitializeAsync());

            // Act

            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadObjectsAsync<Error>().ToListAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadObjectAsync<MetricsData>();
            var result = await reader.ReadToNextAttributeAsync();

            // Assert

            Assert.Null(result);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadObjectAsync_OnString_ReadsString(bool withContext)
        {
            // Arrange

            using var stream = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes("{\"value\":\"string\"}"));

            using var reader = CreateStreamReader(stream, withContext);

            Assert.True(await reader.InitializeAsync());

            // Act/Assert

            await reader.ReadToNextAttributeAsync();
            var value = await reader.ReadObjectAsync<string>();
            Assert.Equal("string", value);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadObjectAsync_OnNumber_ReadsNumber(bool withContext)
        {
            // Arrange

            using var stream = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes("{\"value\":0.105}"));

            using var reader = CreateStreamReader(stream, withContext);

            Assert.True(await reader.InitializeAsync());

            // Act/Assert

            await reader.ReadToNextAttributeAsync();
            var value = await reader.ReadObjectAsync<double>();
            Assert.Equal(0.105, value);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadObjectAsync_OnDateTimeOffset_PreservesTimeZone(bool withContext)
        {
            {
                using var stream = new MemoryStream(
                    System.Text.Encoding.UTF8.GetBytes("{\"value\":\"2021-06-21T10:16:56.9714243+10:00\"}"));

                using var reader = CreateStreamReader(stream, withContext);
                Assert.True(await reader.InitializeAsync());

                await reader.ReadToNextAttributeAsync();
                var value = await reader.ReadObjectAsync<DateTimeOffset>();
                Assert.Equal(10, value.Offset.Hours);
            }
            {
                // check again, just in case the test was run in the +10 timezone and would have given a false positive.
                using var stream = new MemoryStream(
                    System.Text.Encoding.UTF8.GetBytes("{\"value\":\"2021-06-21T10:16:56.9714243-08:00\"}"));

                using var reader = CreateStreamReader(stream, withContext);
                Assert.True(await reader.InitializeAsync());


                await reader.ReadToNextAttributeAsync();
                var value = await reader.ReadObjectAsync<DateTimeOffset>();
                Assert.Equal(-8, value.Offset.Hours);
            }
        }

        #endregion

        #region ReadObjectAsync

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadTokenAsync_ReturnsObject(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-n1ql-error-response-400.json");

            using var reader = CreateStreamReader(stream, withContext);

            Assert.True(await reader.InitializeAsync());

            // Act

            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadObjectsAsync<Error>().ToListAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            var result = await reader.ReadTokenAsync();

            // Assert

            Assert.NotNull(result);

            Assert.Equal((uint) 1, result["errorCount"]!.Value<uint>());

            var data = result.ToObject<MetricsData>();
            Assert.Equal((uint) 1, data.ErrorCount);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadTokenAsync_AfterObject_GetsNextProperty(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Query\query-n1ql-error-response-400.json");

            using var reader = CreateStreamReader(stream, withContext);

            Assert.True(await reader.InitializeAsync());

            // Act

            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadObjectsAsync<Error>().ToListAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadToNextAttributeAsync();
            await reader.ReadTokenAsync();
            var result = await reader.ReadToNextAttributeAsync();

            // Assert

            Assert.Null(result);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadTokenAsync_OnString_ReadsString(bool withContext)
        {
            // Arrange

            using var stream = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes("{\"value\":\"string\"}"));

            using var reader = CreateStreamReader(stream, withContext);

            Assert.True(await reader.InitializeAsync());

            // Act/Assert

            await reader.ReadToNextAttributeAsync();
            var value = await reader.ReadTokenAsync();
            Assert.Equal("string", value.Value<string>());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ReadTokenAsync_OnNumber_ReadsNumber(bool withContext)
        {
            // Arrange

            using var stream = new MemoryStream(
                System.Text.Encoding.UTF8.GetBytes("{\"value\":0.105}"));

            using var reader = CreateStreamReader(stream, withContext);

            Assert.True(await reader.InitializeAsync());

            // Act/Assert

            await reader.ReadToNextAttributeAsync();
            var value = await reader.ReadTokenAsync();
            Assert.Equal(0.105, value.Value<double>());
        }

        #endregion

        #region Value

        [Theory]
        [InlineData(@"Documents\Views\200-success.json", 4L, false)]
        [InlineData(@"Documents\Query\query-n1ql-error-response-400.json", "922edd9a-23d7-4053-8d60-91f7cbc22c83", false)]
        [InlineData(@"Documents\Views\200-success.json", 4L, true)]
        [InlineData(@"Documents\Query\query-n1ql-error-response-400.json", "922edd9a-23d7-4053-8d60-91f7cbc22c83", true)]
        public async Task Value_OnProperty_ReturnsValue(string filename, object expectedFirstProperty, bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(filename);

            using var reader = CreateStreamReader(stream, withContext);

            Assert.True(await reader.InitializeAsync());

            // Act

            await reader.ReadToNextAttributeAsync();
            var result = reader.Value;

            // Assert

            Assert.Equal(expectedFirstProperty, result);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Value_NotOnProperty_ReturnsNull(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Views\200-success.json");

            using var reader = CreateStreamReader(stream, withContext);

            Assert.True(await reader.InitializeAsync());

            // Act

            var result = reader.Value;

            // Assert

            Assert.Null(result);
        }

        #endregion

        #region ValueType

        [Theory]
        [InlineData(@"Documents\Views\200-success.json", typeof(long), false)]
        [InlineData(@"Documents\Query\query-n1ql-error-response-400.json", typeof(string), false)]
        [InlineData(@"Documents\Views\200-success.json", typeof(long), true)]
        [InlineData(@"Documents\Query\query-n1ql-error-response-400.json", typeof(string), true)]
        public async Task ValueType_OnProperty_ReturnsValue(string filename, Type expectedFirstProperty, bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(filename);

            using var reader = CreateStreamReader(stream, withContext);

            Assert.True(await reader.InitializeAsync());

            // Act

            await reader.ReadToNextAttributeAsync();
            var result = reader.ValueType;

            // Assert

            Assert.Equal(expectedFirstProperty, result);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ValueType_NotOnProperty_ReturnsNull(bool withContext)
        {
            // Arrange

            using var stream = ResourceHelper.ReadResourceAsStream(@"Documents\Views\200-success.json");

            using var reader = CreateStreamReader(stream, withContext);

            Assert.True(await reader.InitializeAsync());

            // Act

            var result = reader.ValueType;

            // Assert

            Assert.Null(result);
        }

        #endregion

        #region Helpers

        private static SystemTextJsonStreamReader CreateStreamReader(Stream stream, bool withContext)
        {
            var serializer = withContext
                ? SystemTextJsonSerializer.Create(PersonContext.Default)
                : SystemTextJsonSerializer.Create(new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

            return (SystemTextJsonStreamReader) serializer.CreateJsonStreamReader(stream);
        }

        #endregion
    }
}
