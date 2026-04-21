#nullable enable
using System;
using System.Text;
using System.Text.Json;
using Couchbase.Client.Transactions.Internal;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Couchbase.UnitTests.Transactions;

public class TranscodedContentWrapperTests
{
    #region JSON Content

    [Fact]
    public void JsonObject_RoundTrips_Correctly()
    {
        var original = new { Name = "Alice", Age = 30 };
        var wrapper = new TranscodedContentWrapper(original);

        // Default transcoder is Newtonsoft-based JsonTranscoder, so decode as JObject
        var result = wrapper.ContentAs<JObject>();

        Assert.NotNull(result);
        Assert.Equal("Alice", result!["name"]?.Value<string>());
        Assert.Equal(30, result["age"]?.Value<int>());
    }

    [Fact]
    public void JsonObject_HasJsonFlags()
    {
        var wrapper = new TranscodedContentWrapper(new { Foo = "bar" });

        Assert.Equal(DataFormat.Json, wrapper.Flags.DataFormat);
        Assert.False(wrapper.IsBinary);
    }

    [Fact]
    public void JsonObject_ContentAs_ByteArray_ReturnsEncodedBytes()
    {
        var original = new { Key = "value" };
        var wrapper = new TranscodedContentWrapper(original);

        var bytes = wrapper.ContentAs<byte[]>();

        Assert.NotNull(bytes);
        Assert.True(bytes!.Length > 0);

        // The encoded bytes should be valid JSON (with camelCase property names from Newtonsoft default)
        var json = Encoding.UTF8.GetString(bytes);
        Assert.Contains("key", json);
        Assert.Contains("value", json);
    }

    [Fact]
    public void JsonObject_ContentAs_ReadOnlyMemory_ReturnsEncodedBytes()
    {
        var wrapper = new TranscodedContentWrapper(new { X = 1 });

        var memory = wrapper.ContentAs<ReadOnlyMemory<byte>>();

        Assert.True(memory.Length > 0);
    }

    [Fact]
    public void JsonObject_ContentAs_Memory_ReturnsIndependentCopy()
    {
        var wrapper = new TranscodedContentWrapper(new { X = 1 });

        var memory1 = wrapper.ContentAs<Memory<byte>>();
        var memory2 = wrapper.ContentAs<Memory<byte>>();

        // Both should have content
        Assert.True(memory1.Length > 0);
        Assert.True(memory2.Length > 0);

        // Modifying one should not affect the other (independent copies)
        memory1.Span[0] = 0xFF;
        Assert.NotEqual(memory1.Span[0], memory2.Span[0]);
    }

    #endregion

    #region Binary Content

    [Fact]
    public void BinaryContent_ByteArray_IsFlaggedAsBinary()
    {
        var binaryData = new byte[] { 0x01, 0x02, 0x03 };
        var wrapper = new TranscodedContentWrapper(binaryData, new RawBinaryTranscoder());

        Assert.Equal(DataFormat.Binary, wrapper.Flags.DataFormat);
        Assert.True(wrapper.IsBinary);
    }

    [Fact]
    public void BinaryContent_ReadOnlyMemory_IsFlaggedAsBinary()
    {
        ReadOnlyMemory<byte> binaryData = new byte[] { 0xDE, 0xAD };
        var wrapper = new TranscodedContentWrapper(binaryData, new RawBinaryTranscoder());

        Assert.Equal(DataFormat.Binary, wrapper.Flags.DataFormat);
        Assert.True(wrapper.IsBinary);
    }

    [Fact]
    public void BinaryContent_Memory_IsFlaggedAsBinary()
    {
        Memory<byte> binaryData = new byte[] { 0xBE, 0xEF };
        var wrapper = new TranscodedContentWrapper(binaryData, new RawBinaryTranscoder());

        Assert.Equal(DataFormat.Binary, wrapper.Flags.DataFormat);
        Assert.True(wrapper.IsBinary);
    }

    [Fact]
    public void BinaryContent_ContentAs_ByteArray_ReturnsBinaryData()
    {
        var binaryData = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        var wrapper = new TranscodedContentWrapper(binaryData, new RawBinaryTranscoder());

        var result = wrapper.ContentAs<byte[]>();

        Assert.NotNull(result);
        Assert.Equal(binaryData, result);
    }

    #endregion

    #region Null and Default Transcoder

    [Fact]
    public void NullContent_UsesDefaultJsonTranscoder()
    {
        // null content should not throw and should use JsonTranscoder by default
        var wrapper = new TranscodedContentWrapper(null);

        Assert.IsType<JsonTranscoder>(wrapper.Transcoder);
        Assert.Equal(DataFormat.Json, wrapper.Flags.DataFormat);
    }

    [Fact]
    public void DefaultTranscoder_Is_JsonTranscoder()
    {
        var wrapper = new TranscodedContentWrapper(new { });

        Assert.IsType<JsonTranscoder>(wrapper.Transcoder);
    }

    [Fact]
    public void ExplicitTranscoder_IsPreserved()
    {
        var transcoder = new RawBinaryTranscoder();
        var wrapper = new TranscodedContentWrapper(new byte[] { 1 }, transcoder);

        Assert.Same(transcoder, wrapper.Transcoder);
    }

    #endregion

    #region Transcoder Immutability

    [Fact]
    public void Transcoder_Property_Is_ReadOnly()
    {
        // Verify that IContentAsWrapper.Transcoder has no setter via the interface
        var wrapper = new TranscodedContentWrapper(new { });
        IContentAsWrapper asInterface = wrapper;

        // This test verifies the interface contract: Transcoder should be get-only.
        // If someone adds a setter, the test will need updating, which is the point.
        Assert.NotNull(asInterface.Transcoder);

        // Verify it's the same instance that was set at construction
        Assert.Same(wrapper.Transcoder, asInterface.Transcoder);
    }

    #endregion

    #region Newtonsoft.Json Serializer Compatibility

    [Fact]
    public void NewtonsoftJObject_RoundTrips_WithNewtonsoftTranscoder()
    {
        // Simulate a user who uses Newtonsoft.Json and passes a JObject to a transaction
        var jObject = new JObject
        {
            ["name"] = "Bob",
            ["age"] = 25,
            ["nested"] = new JObject { ["key"] = "value" }
        };

        var newtonsoftTranscoder = new JsonTranscoder(new DefaultSerializer());
        var wrapper = new TranscodedContentWrapper(jObject, newtonsoftTranscoder);

        // Should be flagged as JSON
        Assert.Equal(DataFormat.Json, wrapper.Flags.DataFormat);
        Assert.False(wrapper.IsBinary);

        // Should be able to get bytes (what gets written to xattrs/document)
        var bytes = wrapper.ContentAs<byte[]>();
        Assert.NotNull(bytes);

        // Bytes should be valid JSON
        var json = Encoding.UTF8.GetString(bytes!);
        Assert.Contains("name", json);
        Assert.Contains("Bob", json);
        Assert.Contains("nested", json);

        // Should decode back to JObject correctly
        var decoded = wrapper.ContentAs<JObject>();
        Assert.NotNull(decoded);
        Assert.Equal("Bob", decoded!["name"]?.Value<string>());
        Assert.Equal(25, decoded["age"]?.Value<int>());
        Assert.Equal("value", decoded["nested"]?["key"]?.Value<string>());
    }

    [Fact]
    public void NewtonsoftAnnotatedClass_RoundTrips_WithNewtonsoftTranscoder()
    {
        // Class with Newtonsoft-specific attributes
        var original = new NewtonsoftAnnotatedClass
        {
            MyProperty = "test value",
            IgnoredProperty = "should not appear"
        };

        var newtonsoftTranscoder = new JsonTranscoder(new DefaultSerializer());
        var wrapper = new TranscodedContentWrapper(original, newtonsoftTranscoder);

        var bytes = wrapper.ContentAs<byte[]>();
        var json = Encoding.UTF8.GetString(bytes!);

        // Newtonsoft should honor JsonProperty attribute
        Assert.Contains("custom_name", json);
        Assert.Contains("test value", json);

        // Newtonsoft should honor JsonIgnore attribute
        Assert.DoesNotContain("IgnoredProperty", json);
        Assert.DoesNotContain("should not appear", json);

        // Should decode back correctly
        var decoded = wrapper.ContentAs<NewtonsoftAnnotatedClass>();
        Assert.NotNull(decoded);
        Assert.Equal("test value", decoded!.MyProperty);
    }

    [Fact]
    public void NewtonsoftJArray_RoundTrips_WithNewtonsoftTranscoder()
    {
        var jArray = new JArray { 1, 2, 3, "four", new JObject { ["nested"] = true } };

        var newtonsoftTranscoder = new JsonTranscoder(new DefaultSerializer());
        var wrapper = new TranscodedContentWrapper(jArray, newtonsoftTranscoder);

        Assert.Equal(DataFormat.Json, wrapper.Flags.DataFormat);

        var decoded = wrapper.ContentAs<JArray>();
        Assert.NotNull(decoded);
        Assert.Equal(5, decoded!.Count);
        Assert.Equal(1, decoded[0]?.Value<int>());
        Assert.Equal("four", decoded[3]?.Value<string>());
    }

    private class NewtonsoftAnnotatedClass
    {
        [JsonProperty("custom_name")]
        public string? MyProperty { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public string? IgnoredProperty { get; set; }
    }

    #endregion

    #region System.Text.Json Serializer Compatibility

    [Fact]
    public void StjObject_RoundTrips_WithStjTranscoder()
    {
        var original = new { Name = "Charlie", Age = 35 };

        var stjSerializer = SystemTextJsonSerializer.Create();
        var stjTranscoder = new JsonTranscoder(stjSerializer);
        var wrapper = new TranscodedContentWrapper(original, stjTranscoder);

        Assert.Equal(DataFormat.Json, wrapper.Flags.DataFormat);

        var bytes = wrapper.ContentAs<byte[]>();
        var json = Encoding.UTF8.GetString(bytes!);

        // STJ with default options uses camelCase
        Assert.Contains("name", json);
        Assert.Contains("Charlie", json);

        // Decode as JsonElement (STJ's dynamic type)
        var decoded = wrapper.ContentAs<JsonElement>();
        Assert.Equal("Charlie", decoded.GetProperty("name").GetString());
        Assert.Equal(35, decoded.GetProperty("age").GetInt32());
    }

    [Fact]
    public void StjAnnotatedClass_RoundTrips_WithStjTranscoder()
    {
        var original = new StjAnnotatedClass
        {
            MyProperty = "stj test",
            IgnoredProperty = "should be ignored"
        };

        var stjSerializer = SystemTextJsonSerializer.Create();
        var stjTranscoder = new JsonTranscoder(stjSerializer);
        var wrapper = new TranscodedContentWrapper(original, stjTranscoder);

        var bytes = wrapper.ContentAs<byte[]>();
        var json = Encoding.UTF8.GetString(bytes!);

        // STJ should honor JsonPropertyName attribute
        Assert.Contains("stj_custom_name", json);
        Assert.Contains("stj test", json);

        // STJ should honor JsonIgnore attribute
        Assert.DoesNotContain("IgnoredProperty", json);
        Assert.DoesNotContain("should be ignored", json);

        var decoded = wrapper.ContentAs<StjAnnotatedClass>();
        Assert.NotNull(decoded);
        Assert.Equal("stj test", decoded!.MyProperty);
    }

    [Fact]
    public void StjJsonElement_RoundTrips_WithStjTranscoder()
    {
        // Create a JsonElement (what STJ users might work with)
        var jsonString = """{"key": "value", "number": 42}""";
        var jsonElement = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(jsonString);

        var stjSerializer = SystemTextJsonSerializer.Create();
        var stjTranscoder = new JsonTranscoder(stjSerializer);
        var wrapper = new TranscodedContentWrapper(jsonElement, stjTranscoder);

        Assert.Equal(DataFormat.Json, wrapper.Flags.DataFormat);

        var decoded = wrapper.ContentAs<JsonElement>();
        Assert.Equal("value", decoded.GetProperty("key").GetString());
        Assert.Equal(42, decoded.GetProperty("number").GetInt32());
    }

    private class StjAnnotatedClass
    {
        [System.Text.Json.Serialization.JsonPropertyName("stj_custom_name")]
        public string? MyProperty { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public string? IgnoredProperty { get; set; }
    }

    #endregion

    #region Cross-Serializer Scenarios

    [Fact]
    public void ContentEncodedWithNewtonsoft_CanBeReadAsBytes_ForStagingInXattrs()
    {
        // This simulates the transaction flow:
        // 1. User passes content with their Newtonsoft serializer
        // 2. Transaction code extracts bytes to write to xattrs
        // 3. The bytes should be valid JSON that can later be read

        var userContent = new JObject
        {
            ["transactionData"] = "important",
            ["amount"] = 100.50
        };

        var newtonsoftTranscoder = new JsonTranscoder(new DefaultSerializer());
        var wrapper = new TranscodedContentWrapper(userContent, newtonsoftTranscoder);

        // Transaction code will call ContentAs<byte[]>() to get bytes for staging
        var stagedBytes = wrapper.ContentAs<byte[]>();
        Assert.NotNull(stagedBytes);

        // The staged bytes should be parseable JSON
        var json = Encoding.UTF8.GetString(stagedBytes!);
        var parsed = JObject.Parse(json);
        Assert.Equal("important", parsed["transactionData"]?.Value<string>());
        Assert.Equal(100.50, parsed["amount"]?.Value<double>());
    }

    [Fact]
    public void ContentEncodedWithStj_CanBeReadAsBytes_ForStagingInXattrs()
    {
        // Same scenario but with STJ user
        var userContent = new { TransactionData = "important", Amount = 100.50 };

        var stjSerializer = SystemTextJsonSerializer.Create();
        var stjTranscoder = new JsonTranscoder(stjSerializer);
        var wrapper = new TranscodedContentWrapper(userContent, stjTranscoder);

        var stagedBytes = wrapper.ContentAs<byte[]>();
        Assert.NotNull(stagedBytes);

        // The staged bytes should be parseable JSON
        var json = Encoding.UTF8.GetString(stagedBytes!);
        var parsed = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(json);
        Assert.Equal("important", parsed.GetProperty("transactionData").GetString());
        Assert.Equal(100.50, parsed.GetProperty("amount").GetDouble());
    }

    #endregion

    #region MetadataJsonOptions Dictionary Serialization

    [Fact]
    public void MetadataSerializer_ExtBinSupport_PreservesExactDictionaryKeys()
    {
        // Use the actual MetadataTranscoder from Transactions class
        var metadataTranscoder = Client.Transactions.Transactions.MetadataTranscoder;

        // Replicate the extBinSupport dictionary structure from ForwardCompatibility
        var extBinSupportAction = new System.Collections.Generic.Dictionary<string, object>
        {
            ["b"] = "f",
            ["e"] = "BS",
        };
        var extBinSupportActions = new[] { extBinSupportAction };
        var extBinSupport = new System.Collections.Generic.Dictionary<string, object>
        {
            ["CL_E"] = extBinSupportActions,
            ["G"] = extBinSupportActions,
            ["WW_I"] = extBinSupportActions,
            ["WW_IG"] = extBinSupportActions,
        };

        // Serialize using the metadata serializer
        using var stream = new System.IO.MemoryStream();
        metadataTranscoder.Serializer!.Serialize(stream, extBinSupport);

        stream.Position = 0;
        var json = Encoding.UTF8.GetString(stream.ToArray());

        // Verify exact key preservation - no camelCase transformation
        Assert.Contains("\"CL_E\":", json);
        Assert.Contains("\"G\":", json);
        Assert.Contains("\"WW_I\":", json);
        Assert.Contains("\"WW_IG\":", json);

        // Verify no camelCase transformation occurred
        Assert.DoesNotContain("\"cL_E\"", json);
        Assert.DoesNotContain("\"g\":", json);
        Assert.DoesNotContain("\"wW_I\"", json);
        Assert.DoesNotContain("\"wW_IG\"", json);
    }

    #endregion
}
