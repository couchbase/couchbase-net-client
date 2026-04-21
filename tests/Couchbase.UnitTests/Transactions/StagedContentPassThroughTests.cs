#nullable enable
using System.Text;
using System.Text.Json;
using Couchbase.Client.Transactions.Internal;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Couchbase.UnitTests.Transactions;

/// <summary>
/// Verifies that the JsonElement pass-through pattern used in DocumentRepository is lossless:
/// user content bytes → JsonElement (STJ parse) → bytes must preserve all field data.
/// This guards against serializer mismatches introduced by the Newtonsoft → STJ metadata migration.
/// </summary>
public class StagedContentPassThroughTests
{
    private record SampleDocument(string Name, int Count, double Amount, string? Notes);

    #region JsonElement pass-through losslessness

    [Fact]
    public void NewtonsoftEncodedBytes_ParseToJsonElement_RoundTrip_Losslessly()
    {
        var userContent = new JObject
        {
            ["name"] = "Alice",
            ["count"] = 42,
            ["amount"] = 100.50,
            ["notes"] = "test note"
        };

        var newtonsoftTranscoder = new JsonTranscoder(new DefaultSerializer());
        var wrapper = new TranscodedContentWrapper(userContent, newtonsoftTranscoder);
        var stagedBytes = wrapper.ContentAs<byte[]>()!;

        // Simulate what DocumentRepository does: parse staged bytes into a JsonElement
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(stagedBytes);

        // Verify no data loss
        Assert.Equal("Alice", jsonElement.GetProperty("name").GetString());
        Assert.Equal(42, jsonElement.GetProperty("count").GetInt32());
        Assert.Equal(100.50, jsonElement.GetProperty("amount").GetDouble(), precision: 10);
        Assert.Equal("test note", jsonElement.GetProperty("notes").GetString());
    }

    [Fact]
    public void StjEncodedBytes_ParseToJsonElement_RoundTrip_Losslessly()
    {
        var userContent = new SampleDocument("Bob", 99, 3.14, null);

        var stjSerializer = SystemTextJsonSerializer.Create();
        var stjTranscoder = new JsonTranscoder(stjSerializer);
        var wrapper = new TranscodedContentWrapper(userContent, stjTranscoder);
        var stagedBytes = wrapper.ContentAs<byte[]>()!;

        var jsonElement = JsonSerializer.Deserialize<JsonElement>(stagedBytes);

        Assert.Equal("Bob", jsonElement.GetProperty("name").GetString());
        Assert.Equal(99, jsonElement.GetProperty("count").GetInt32());
        Assert.Equal(3.14, jsonElement.GetProperty("amount").GetDouble(), precision: 10);
    }

    [Fact]
    public void JsonElement_Reserializes_ToIdenticalBytes()
    {
        // Verify that JsonElement is truly verbatim: serialize → parse → serialize produces same bytes.
        // This is the core invariant that makes the staging pass-through safe.
        const string originalJson = """{"name":"Carol","count":7,"tags":["a","b","c"]}""";
        var originalBytes = Encoding.UTF8.GetBytes(originalJson);

        var jsonElement = JsonSerializer.Deserialize<JsonElement>(originalBytes);
        var reserialized = JsonSerializer.SerializeToUtf8Bytes(jsonElement);

        Assert.Equal(originalJson, Encoding.UTF8.GetString(reserialized));
    }

    [Fact]
    public void NewtonsoftEncodedBytes_ThenJsonElement_ThenReserialized_PreservesContent()
    {
        // Full staging pipeline:
        // Newtonsoft user encodes content → bytes → JsonElement (repository) → re-serialized bytes
        // The re-serialized bytes must decode back to the same document.
        var userContent = new JObject
        {
            ["transactionData"] = "important",
            ["amount"] = 9999,
            ["nested"] = new JObject { ["key"] = "value" }
        };

        var newtonsoftTranscoder = new JsonTranscoder(new DefaultSerializer());
        var wrapper = new TranscodedContentWrapper(userContent, newtonsoftTranscoder);
        var stagedBytes = wrapper.ContentAs<byte[]>()!;

        // DocumentRepository staging: bytes → JsonElement
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(stagedBytes);
        var reserializedBytes = JsonSerializer.SerializeToUtf8Bytes(jsonElement);

        // The re-serialized bytes should decode back to the original data
        var recovered = JObject.Parse(Encoding.UTF8.GetString(reserializedBytes));
        Assert.Equal("important", recovered["transactionData"]?.Value<string>());
        Assert.Equal(9999, recovered["amount"]?.Value<int>());
        Assert.Equal("value", recovered["nested"]?["key"]?.Value<string>());
    }

    #endregion

    #region Cross-serializer byte extraction

    [Fact]
    public void NewtonsoftUser_BytesExtractedForStaging_AreValidJson()
    {
        // A Newtonsoft user's content must produce valid JSON bytes when extracted for staging.
        // This bytes extraction is the first step of DocumentRepository's staging pipeline.
        var userContent = new JObject { ["key"] = "value", ["num"] = 123 };
        var newtonsoftTranscoder = new JsonTranscoder(new DefaultSerializer());
        var wrapper = new TranscodedContentWrapper(userContent, newtonsoftTranscoder);

        var stagedBytes = wrapper.ContentAs<byte[]>()!;

        Assert.NotEmpty(stagedBytes);
        // Must be parseable as JSON by STJ (what DocumentRepository does)
        var element = JsonSerializer.Deserialize<JsonElement>(stagedBytes);
        Assert.Equal(JsonValueKind.Object, element.ValueKind);
        Assert.Equal("value", element.GetProperty("key").GetString());
    }

    [Fact]
    public void StjUser_BytesExtractedForStaging_AreValidJson()
    {
        var userContent = new SampleDocument("Dan", 5, 1.5, null);
        var stjSerializer = SystemTextJsonSerializer.Create();
        var stjTranscoder = new JsonTranscoder(stjSerializer);
        var wrapper = new TranscodedContentWrapper(userContent, stjTranscoder);

        var stagedBytes = wrapper.ContentAs<byte[]>()!;

        Assert.NotEmpty(stagedBytes);
        var element = JsonSerializer.Deserialize<JsonElement>(stagedBytes);
        Assert.Equal(JsonValueKind.Object, element.ValueKind);
        Assert.Equal("Dan", element.GetProperty("name").GetString());
    }

    #endregion
}
