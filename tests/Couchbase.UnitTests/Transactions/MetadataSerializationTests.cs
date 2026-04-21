#nullable enable
using System.Text.Json;
using Couchbase.Client.Transactions;
using Couchbase.Client.Transactions.Components;
using Couchbase.Client.Transactions.DataModel;
using Couchbase.Client.Transactions.Forwards;
using Couchbase.Client.Transactions.Support;
using Xunit;

namespace Couchbase.UnitTests.Transactions;

public class MetadataSerializationTests
{
    private static readonly JsonSerializerOptions Opts = Client.Transactions.Transactions.MetadataJsonOptions;

    #region AtrEntry

    [Fact]
    public void AtrEntry_CreateFrom_JsonElement_Deserializes_BasicFields()
    {
        const string json = """{"tid":"txn-123","st":"PENDING","tst":"0x000058a71dd25c15","exp":15000,"ins":[],"rep":[],"rem":[]}""";
        var element = JsonSerializer.Deserialize<JsonElement>(json);

        var entry = AtrEntry.CreateFrom(element);

        Assert.NotNull(entry);
        Assert.Equal("txn-123", entry!.TransactionId);
        Assert.Equal(AttemptStates.PENDING, entry.State);
        Assert.Equal("0x000058a71dd25c15", entry.TimestampStartCas);
        Assert.Equal(15000, entry.ExpiresAfterMsecs);
        Assert.Empty(entry.InsertedIds);
    }

    [Fact]
    public void AtrEntry_CreateFrom_JsonElement_Deserializes_DocRecords()
    {
        const string json = """{"tid":"txn-456","st":"COMMITTED","ins":[{"bkt":"b","scp":"s","col":"c","id":"doc1"}],"rep":[],"rem":[]}""";
        var element = JsonSerializer.Deserialize<JsonElement>(json);

        var entry = AtrEntry.CreateFrom(element);

        Assert.NotNull(entry);
        Assert.Single(entry!.InsertedIds);
        Assert.Equal("doc1", entry.InsertedIds[0].Id);
        Assert.Equal("b", entry.InsertedIds[0].BucketName);
    }

    [Fact]
    public void AtrEntry_Deserializes_BasicFields()
    {
        const string json = """{"tid":"txn-123","st":"PENDING","tst":"0x000058a71dd25c15","exp":15000,"ins":[],"rep":[],"rem":[]}""";

        var entry = JsonSerializer.Deserialize<AtrEntry>(json, Opts);

        Assert.NotNull(entry);
        Assert.Equal("txn-123", entry!.TransactionId);
        Assert.Equal(AttemptStates.PENDING, entry.State);
        Assert.Equal("0x000058a71dd25c15", entry.TimestampStartCas);
        Assert.Equal(15000, entry.ExpiresAfterMsecs);
        Assert.Empty(entry.InsertedIds);
    }

    [Fact]
    public void AtrEntry_Deserializes_DocRecords()
    {
        const string json = """{"tid":"txn-456","st":"COMMITTED","ins":[{"bkt":"b","scp":"s","col":"c","id":"doc1"}],"rep":[{"bkt":"b2","scp":"s2","col":"c2","id":"doc2"}],"rem":[]}""";

        var entry = JsonSerializer.Deserialize<AtrEntry>(json, Opts);

        Assert.NotNull(entry);
        Assert.Equal(AttemptStates.COMMITTED, entry!.State);
        Assert.Single(entry.InsertedIds);
        Assert.Equal("doc1", entry.InsertedIds[0].Id);
        Assert.Equal("b", entry.InsertedIds[0].BucketName);
        Assert.Single(entry.ReplacedIds);
        Assert.Equal("doc2", entry.ReplacedIds[0].Id);
        Assert.Empty(entry.RemovedIds);
    }

    [Fact]
    public void AtrEntry_Serializes_NullFieldsOmitted_EmptyListsPresent()
    {
        var entry = new AtrEntry
        {
            TransactionId = "txn-789",
            State = AttemptStates.PENDING
        };

        var json = JsonSerializer.Serialize(entry, Opts);

        Assert.Contains("\"tid\":\"txn-789\"", json);
        Assert.Contains("\"st\":\"PENDING\"", json);
        // null fields omitted
        Assert.DoesNotContain("\"tst\":", json);
        Assert.DoesNotContain("\"exp\":", json);
        Assert.DoesNotContain("\"fc\":", json);
        Assert.DoesNotContain("\"d\":", json);
        // empty lists (not null) are included
        Assert.Contains("\"ins\":[]", json);
        Assert.Contains("\"rep\":[]", json);
        Assert.Contains("\"rem\":[]", json);
    }

    [Theory]
    [InlineData("PENDING", AttemptStates.PENDING)]
    [InlineData("COMMITTED", AttemptStates.COMMITTED)]
    [InlineData("ROLLED_BACK", AttemptStates.ROLLED_BACK)]
    [InlineData("COMPLETED", AttemptStates.COMPLETED)]
    [InlineData("ABORTED", AttemptStates.ABORTED)]
    [InlineData("NOTHING_WRITTEN", AttemptStates.NOTHING_WRITTEN)]
    public void AttemptStates_RoundTrips_AsAllCapsString(string jsonValue, AttemptStates expected)
    {
        var json = $"{{\"st\":\"{jsonValue}\"}}";
        var entry = JsonSerializer.Deserialize<AtrEntry>(json, Opts);

        Assert.NotNull(entry);
        Assert.Equal(expected, entry!.State);

        var serialized = JsonSerializer.Serialize(new AtrEntry { State = expected }, Opts);
        Assert.Contains($"\"st\":\"{jsonValue}\"", serialized);
    }

    #endregion

    #region DocumentMetadata

    [Fact]
    public void DocumentMetadata_Deserializes_WithInternalSetters()
    {
        // Verifies [JsonInclude] enables deserialization into internal set properties
        const string json = """{"CAS":"0xaabbccdd","revid":"1-abc","exptime":3600,"value_crc32c":"0x00000000"}""";

        var meta = JsonSerializer.Deserialize<DocumentMetadata>(json, Opts);

        Assert.NotNull(meta);
        Assert.Equal("0xaabbccdd", meta!.Cas);
        Assert.Equal("1-abc", meta.RevId);
        Assert.Equal(3600ul, meta.ExpTime);
        Assert.Equal("0x00000000", meta.Crc32c);
    }

    [Fact]
    public void DocumentMetadata_MissingNullableFields_DeserializeToNull()
    {
        const string json = """{"CAS":"0x1234"}""";

        var meta = JsonSerializer.Deserialize<DocumentMetadata>(json, Opts);

        Assert.NotNull(meta);
        Assert.Equal("0x1234", meta!.Cas);
        Assert.Null(meta.RevId);
        Assert.Null(meta.ExpTime);
        Assert.Null(meta.Crc32c);
    }

    [Fact]
    public void DocumentMetadata_Serializes_NullFieldsOmitted()
    {
        var meta = new DocumentMetadata { Cas = "0xdeadbeef" };

        var json = JsonSerializer.Serialize(meta, Opts);

        Assert.Contains("\"CAS\":\"0xdeadbeef\"", json);
        Assert.DoesNotContain("revid", json);
        Assert.DoesNotContain("exptime", json);
        Assert.DoesNotContain("value_crc32c", json);
    }

    #endregion

    #region ClientRecordEntry

    [Fact]
    public void ClientRecordEntry_Deserializes_FromKnownJson()
    {
        const string json = """{"heartbeat_ms":"0x000058a71dd25c15","expires_ms":60000,"num_atrs":1024}""";

        var entry = JsonSerializer.Deserialize<ClientRecordEntry>(json, Opts);

        Assert.NotNull(entry);
        Assert.Equal("0x000058a71dd25c15", entry!.HeartbeatMutationCas);
        Assert.Equal(60000L, entry.ExpiresMilliseconds);
        Assert.Equal(1024, entry.NumAtrs);
    }

    [Fact]
    public void ClientRecordEntry_Serializes_WithCorrectFieldNames()
    {
        var entry = new ClientRecordEntry
        {
            HeartbeatMutationCas = "${Mutation.CAS}",
            ExpiresMilliseconds = 120000L,
            NumAtrs = 512
        };

        var json = JsonSerializer.Serialize(entry, Opts);

        Assert.Contains("\"heartbeat_ms\"", json);
        Assert.Contains("\"expires_ms\"", json);
        Assert.Contains("\"num_atrs\":512", json);
    }

    #endregion

    #region TransactionXattrs

    [Fact]
    public void TransactionXattrs_Deserializes_NestedTypes()
    {
        const string json = """
            {
                "id":{"txn":"txn-id","atmpt":"atmpt-id","op":"op-id"},
                "atr":{"id":"atr1","bkt":"bucket","scp":"_default","coll":"_default"},
                "op":{"type":"insert"},
                "restore":{"CAS":"0xdeadbeef"}
            }
            """;

        var xattrs = JsonSerializer.Deserialize<TransactionXattrs>(json, Opts);

        Assert.NotNull(xattrs);
        Assert.Equal("txn-id", xattrs!.Id?.Transactionid);
        Assert.Equal("atmpt-id", xattrs.Id?.AttemptId);
        Assert.Equal("op-id", xattrs.Id?.OperationId);
        Assert.Equal("atr1", xattrs.AtrRef?.Id);
        Assert.Equal("bucket", xattrs.AtrRef?.BucketName);
        Assert.Equal("insert", xattrs.Operation?.Type);
        Assert.Equal("0xdeadbeef", xattrs.RestoreMetadata?.Cas);
    }

    [Fact]
    public void TransactionXattrs_Serializes_NullFieldsOmitted()
    {
        var xattrs = new TransactionXattrs
        {
            Id = new CompositeId { Transactionid = "txn-id", AttemptId = "atmpt-id" }
        };

        var json = JsonSerializer.Serialize(xattrs, Opts);

        Assert.Contains("\"id\":", json);
        Assert.DoesNotContain("\"atr\":", json);
        Assert.DoesNotContain("\"op\":", json);
        Assert.DoesNotContain("\"restore\":", json);
    }

    #endregion

    #region CompatibilityCheck

    [Fact]
    public void CompatibilityCheck_AcceptsNumber_ForProtocolVersion()
    {
        const string json = """{"p":2.0,"b":"r"}""";

        var check = JsonSerializer.Deserialize<CompatibilityCheck>(json, Opts);

        Assert.NotNull(check);
        Assert.Equal(2.0m, check!.ProtocolVersion);
        Assert.Equal('r', check.Behavior);
    }

    [Fact]
    public void CompatibilityCheck_AcceptsString_ForProtocolVersion()
    {
        // [JsonNumberHandling(AllowReadingFromString)] lets "2.0" deserialize as decimal
        const string json = """{"p":"2.0","ra":"5000"}""";

        var check = JsonSerializer.Deserialize<CompatibilityCheck>(json, Opts);

        Assert.NotNull(check);
        Assert.Equal(2.0m, check!.ProtocolVersion);
        Assert.Equal(5000, check.RetryDelay);
    }

    [Fact]
    public void CompatibilityCheck_AcceptsNumber_ForRetryDelay()
    {
        const string json = """{"ra":1000}""";

        var check = JsonSerializer.Deserialize<CompatibilityCheck>(json, Opts);

        Assert.NotNull(check);
        Assert.Equal(1000, check!.RetryDelay);
    }

    #endregion
}
