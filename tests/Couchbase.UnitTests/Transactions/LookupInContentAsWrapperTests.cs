#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Couchbase.Client.Transactions.Internal;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Transactions;

/// <summary>
/// Verifies that LookupInContentAsWrapper.ContentAs&lt;T&gt;() uses its own Transcoder
/// (the user data transcoder) rather than the LookupInResult's metadata serializer.
/// This was a bug fix in the Newtonsoft → STJ metadata migration.
/// </summary>
public class LookupInContentAsWrapperTests
{
    private class SampleDocument
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    private class NewtonsoftSampleDocument
    {
        [Newtonsoft.Json.JsonProperty("name")]
        public string? Name { get; set; }

        [Newtonsoft.Json.JsonProperty("count")]
        public int Count { get; set; }
    }

    private static Mock<ILookupInResultInternal> BuildMock(ReadOnlyMemory<byte> specBytes)
    {
        var spec = new LookupInSpec
        {
            Bytes = specBytes,
            PathFlags = SubdocPathFlags.None
        };

        var mock = new Mock<ILookupInResultInternal>();
        mock.Setup(m => m.Specs).Returns(new List<LookupInSpec> { spec });
        mock.Setup(m => m.Flags).Returns(new Flags { DataFormat = DataFormat.Json });
        return mock;
    }

    private static ReadOnlyMemory<byte> EncodeWithNewtonsoft(object content)
    {
        var serializer = new DefaultSerializer();
        using var stream = new System.IO.MemoryStream();
        serializer.Serialize(stream, content);
        return stream.ToArray();
    }

    private static ReadOnlyMemory<byte> EncodeWithStj(object content)
    {
        var serializer = SystemTextJsonSerializer.Create();
        using var stream = new System.IO.MemoryStream();
        serializer.Serialize(stream, content);
        return stream.ToArray();
    }

    #region Transcoder isolation

    [Fact]
    public void ContentAs_UsesOwnTranscoder_NotLookupInResultSerializer()
    {
        // Bytes encoded with Newtonsoft; wrapper is constructed with a Newtonsoft transcoder.
        // This verifies the fix: ContentAs<T>() must use wrapper.Transcoder, not the LookupIn's serializer.
        var original = new NewtonsoftSampleDocument { Name = "Alice", Count = 42 };
        var encodedBytes = EncodeWithNewtonsoft(original);

        var mock = BuildMock(encodedBytes);
        var newtonsoftTranscoder = new JsonTranscoder(new DefaultSerializer());
        var wrapper = new LookupInContentAsWrapper(mock.Object, 0, newtonsoftTranscoder);

        var decoded = wrapper.ContentAs<NewtonsoftSampleDocument>();

        Assert.NotNull(decoded);
        Assert.Equal("Alice", decoded!.Name);
        Assert.Equal(42, decoded.Count);
    }

    [Fact]
    public void ContentAs_WithStjTranscoder_DecodesStjEncodedBytes()
    {
        var original = new SampleDocument { Name = "Bob", Count = 99 };
        var encodedBytes = EncodeWithStj(original);

        var mock = BuildMock(encodedBytes);
        var stjTranscoder = new JsonTranscoder(SystemTextJsonSerializer.Create());
        var wrapper = new LookupInContentAsWrapper(mock.Object, 0, stjTranscoder);

        var decoded = wrapper.ContentAs<SampleDocument>();

        Assert.NotNull(decoded);
        Assert.Equal("Bob", decoded!.Name);
        Assert.Equal(99, decoded.Count);
    }

    #endregion

    #region Byte pass-through overloads

    [Fact]
    public void ContentAs_ByteArray_ReturnsRawBytes_WithoutInvokingTranscoder()
    {
        var expectedBytes = System.Text.Encoding.UTF8.GetBytes("""{"name":"Charlie","count":7}""");
        var mock = BuildMock(expectedBytes);

        // Use a transcoder spy to confirm it's never called for byte[]
        var transcoderSpy = new Mock<ITypeTranscoder>();
        var wrapper = new LookupInContentAsWrapper(mock.Object, 0, transcoderSpy.Object);

        var result = wrapper.ContentAs<byte[]>();

        Assert.NotNull(result);
        Assert.Equal(expectedBytes, result);
        transcoderSpy.Verify(t => t.Decode<byte[]>(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<Flags>(), It.IsAny<OpCode>()), Times.Never);
    }

    [Fact]
    public void ContentAs_ReadOnlyMemory_ReturnsRawBytes_WithoutInvokingTranscoder()
    {
        var expectedBytes = System.Text.Encoding.UTF8.GetBytes("""{"x":1}""");
        var mock = BuildMock(expectedBytes);

        var transcoderSpy = new Mock<ITypeTranscoder>();
        var wrapper = new LookupInContentAsWrapper(mock.Object, 0, transcoderSpy.Object);

        var result = wrapper.ContentAs<System.ReadOnlyMemory<byte>>();

        Assert.Equal(expectedBytes, result.ToArray());
        transcoderSpy.Verify(t => t.Decode<System.ReadOnlyMemory<byte>>(It.IsAny<ReadOnlyMemory<byte>>(), It.IsAny<Flags>(), It.IsAny<OpCode>()), Times.Never);
    }

    #endregion

    #region Non-JSON content

    [Fact]
    public void Constructor_Throws_WhenGivenNonInternalResult()
    {
        // If someone passes an ILookupInResult that isn't ILookupInResultInternal,
        // the constructor must throw rather than silently producing wrong results.
        var plainMock = new Mock<ILookupInResult>();

        Assert.Throws<Couchbase.Core.Exceptions.InvalidArgumentException>(
            () => new LookupInContentAsWrapper(plainMock.Object, 0));
    }

    #endregion

    #region Transcoder property

    [Fact]
    public void Transcoder_Property_Reflects_ConstructorArgument()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("{}");
        var mock = BuildMock(bytes);
        var transcoder = new JsonTranscoder(new DefaultSerializer());

        var wrapper = new LookupInContentAsWrapper(mock.Object, 0, transcoder);

        Assert.Same(transcoder, wrapper.Transcoder);
    }

    [Fact]
    public void Transcoder_DefaultsToJsonTranscoder_WhenNotProvided()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("{}");
        var mock = BuildMock(bytes);

        var wrapper = new LookupInContentAsWrapper(mock.Object, 0);

        Assert.IsType<JsonTranscoder>(wrapper.Transcoder);
    }

    #endregion
}
