#nullable enable
using System.Collections.Generic;
using System.IO;
using Couchbase.Client.Transactions.Internal;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Xunit;

namespace Couchbase.UnitTests.Transactions;

/// <summary>
/// NCBC-4261: FixedFlagsTranscoder pins the flags written to a document (used on the raw-insert
/// commit paths, which have no flags option) while delegating byte encoding/decoding to the inner
/// transcoder.
/// </summary>
public class FixedFlagsTranscoderTests
{
    private static JsonTranscoder InnerJson() => new(SystemTextJsonSerializer.Create());

    [Fact]
    public void GetFormat_AlwaysReturnsFixedFlags_IgnoringContentType()
    {
        // Inner would report Json for an object and Binary for a byte[]; the decorator must not.
        var fixedFlags = new Flags { DataFormat = DataFormat.Binary, TypeCode = System.TypeCode.String };
        var transcoder = new FixedFlagsTranscoder(InnerJson(), fixedFlags);

        var forObject = transcoder.GetFormat(new { a = 1 });
        var forBytes = transcoder.GetFormat(new byte[] { 1, 2, 3 });

        Assert.Equal(DataFormat.Binary, forObject.DataFormat);
        Assert.Equal(System.TypeCode.String, forObject.TypeCode);
        Assert.Equal(DataFormat.Binary, forBytes.DataFormat);
        Assert.Equal(System.TypeCode.String, forBytes.TypeCode);
    }

    [Fact]
    public void Encode_DelegatesToInner()
    {
        var inner = InnerJson();
        var content = new { key = "value" };
        var flags = new Flags { DataFormat = DataFormat.Json, TypeCode = System.TypeCode.Object };
        var transcoder = new FixedFlagsTranscoder(inner, Flags.JsonCommonFlags);

        using var innerStream = new MemoryStream();
        inner.Encode(innerStream, content, flags, OpCode.Set);

        using var decoratedStream = new MemoryStream();
        transcoder.Encode(decoratedStream, content, flags, OpCode.Set);

        Assert.Equal(innerStream.ToArray(), decoratedStream.ToArray());
    }

    [Fact]
    public void Decode_DelegatesToInner_RoundTrips()
    {
        var inner = InnerJson();
        var flags = new Flags { DataFormat = DataFormat.Json, TypeCode = System.TypeCode.Object };
        var transcoder = new FixedFlagsTranscoder(inner, Flags.JsonCommonFlags);

        using var stream = new MemoryStream();
        inner.Encode(stream, new { number = 42 }, flags, OpCode.Set);

        var decoded = transcoder.Decode<Dictionary<string, int>>(stream.ToArray(), flags, OpCode.Get);

        Assert.NotNull(decoded);
        Assert.Equal(42, decoded!["number"]);
    }

    [Fact]
    public void Serializer_DelegatesToInner()
    {
        var inner = InnerJson();
        var transcoder = new FixedFlagsTranscoder(inner, Flags.JsonCommonFlags);

        Assert.Same(inner.Serializer, transcoder.Serializer);

        var newSerializer = SystemTextJsonSerializer.Create();
        transcoder.Serializer = newSerializer;
        Assert.Same(newSerializer, inner.Serializer);
    }
}
