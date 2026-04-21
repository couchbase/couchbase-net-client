#nullable enable
using System.Text.Json;
using Couchbase.Client.Transactions;
using Couchbase.Client.Transactions.DataModel;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Couchbase.UnitTests.Transactions;

public class TransactionGetResultTests
{
    private static ICouchbaseCollection MockCollection()
    {
        var mockBucket = new Mock<IBucket>();
        mockBucket.Setup(b => b.Name).Returns("test-bucket");
        var mockScope = new Mock<IScope>();
        mockScope.Setup(s => s.Name).Returns("_default");
        mockScope.Setup(s => s.Bucket).Returns(mockBucket.Object);
        var mockCollection = new Mock<ICouchbaseCollection>();
        mockCollection.Setup(c => c.Name).Returns("_default");
        mockCollection.Setup(c => c.Scope).Returns(mockScope.Object);
        return mockCollection.Object;
    }

    #region FromQueryGet

    [Fact]
    public void FromQueryGet_WithNewtonsoftTranscoder_ContentAs_DecodesDocument()
    {
        // Simulate the Newtonsoft path: query result's doc is a JObject
        var doc = new JObject { ["name"] = "Alice", ["count"] = 42 };
        var queryResult = new QueryGetResult("12345", doc, null);
        var transcoder = new JsonTranscoder(new DefaultSerializer());

        var result = TransactionGetResult.FromQueryGet(MockCollection(), "doc-id", queryResult, transcoder);

        var decoded = result.ContentAs<JObject>();
        Assert.NotNull(decoded);
        Assert.Equal("Alice", decoded!["name"]?.Value<string>());
        Assert.Equal(42, decoded["count"]?.Value<int>());
    }

    [Fact]
    public void FromQueryGet_WithStjTranscoder_ContentAs_DecodesDocument()
    {
        // Simulate the STJ path: query result's doc is a JsonElement
        var doc = JsonSerializer.Deserialize<JsonElement>("""{"name":"Bob","count":99}""");
        var queryResult = new QueryGetResult("67890", doc, null);
        var transcoder = new JsonTranscoder(SystemTextJsonSerializer.Create());

        var result = TransactionGetResult.FromQueryGet(MockCollection(), "doc-id", queryResult, transcoder);

        var decoded = result.ContentAs<JsonElement>();
        Assert.Equal("Bob", decoded.GetProperty("name").GetString());
        Assert.Equal(99, decoded.GetProperty("count").GetInt32());
    }

    [Fact]
    public void FromQueryGet_CasIsParsedFromScas()
    {
        var doc = new JObject { ["x"] = 1 };
        var queryResult = new QueryGetResult("9999999999999999999", doc, null);

        var result = TransactionGetResult.FromQueryGet(MockCollection(), "doc-id", queryResult, new JsonTranscoder());

        Assert.Equal(9999999999999999999UL, result.Cas);
    }

    #endregion

    #region FromQueryInsert

    [Fact]
    public void FromQueryInsert_WithNewtonsoftTranscoder_ContentAs_DecodesDocument()
    {
        var originalDoc = new JObject { ["name"] = "Carol", ["value"] = 7 };
        var insertResult = new QueryInsertResult("11111");
        var transcoder = new JsonTranscoder(new DefaultSerializer());

        var result = TransactionGetResult.FromQueryInsert(MockCollection(), "doc-id", originalDoc, insertResult, transcoder);

        var decoded = result.ContentAs<JObject>();
        Assert.NotNull(decoded);
        Assert.Equal("Carol", decoded!["name"]?.Value<string>());
    }

    [Fact]
    public void FromQueryInsert_WithStjTranscoder_ContentAs_DecodesDocument()
    {
        var originalDoc = new { name = "Dave", value = 42 };
        var insertResult = new QueryInsertResult("22222");
        var transcoder = new JsonTranscoder(SystemTextJsonSerializer.Create());

        var result = TransactionGetResult.FromQueryInsert(MockCollection(), "doc-id", originalDoc, insertResult, transcoder);

        var decoded = result.ContentAs<JsonElement>();
        Assert.Equal("Dave", decoded.GetProperty("name").GetString());
        Assert.Equal(42, decoded.GetProperty("value").GetInt32());
    }

    #endregion
}
