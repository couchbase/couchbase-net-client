#nullable enable
using System.Text.Json;
using System.Threading.Tasks;
using Couchbase.Client.Transactions.Error.External;
using Couchbase.Client.Transactions.Forwards;
using Xunit;

namespace Couchbase.UnitTests.Transactions;

public class ForwardCompatibilityTests
{
    private static JsonElement ParseFc(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    [Fact]
    public async Task Check_NullFc_DoesNotThrow()
    {
        await ForwardCompatibility.Check(null, ForwardCompatibility.Gets, null);
    }

    [Fact]
    public async Task Check_JsonNullFc_DoesNotThrow()
    {
        var fc = ParseFc("null");
        await ForwardCompatibility.Check(null, ForwardCompatibility.Gets, fc);
    }

    [Fact]
    public async Task Check_NoMatchingInteractionPoint_DoesNotThrow()
    {
        // fc has an entry but for a different interaction point
        var fc = ParseFc("""{"WW_R": [{"p": 99.0, "b": "f"}]}""");
        await ForwardCompatibility.Check(null, ForwardCompatibility.Gets, fc);
    }

    [Fact]
    public async Task Check_CompatibleProtocolVersion_DoesNotThrow()
    {
        // SupportedVersion is 2.1 — a lower version must not trigger a failure
        var fc = ParseFc("""{"G": [{"p": 2.0, "b": "f"}]}""");
        await ForwardCompatibility.Check(null, ForwardCompatibility.Gets, fc);
    }

    [Fact]
    public async Task Check_IncompatibleProtocolVersion_ThrowsOperationFailed()
    {
        var fc = ParseFc("""{"G": [{"p": 99.0, "b": "f"}]}""");

        var ex = await Assert.ThrowsAsync<TransactionOperationFailedException>(
            () => ForwardCompatibility.Check(null, ForwardCompatibility.Gets, fc));

        Assert.False(ex.RetryTransaction);
    }

    [Fact]
    public async Task Check_IncompatibleProtocolVersion_WithRetryBehavior_ThrowsWithRetry()
    {
        // ra:0 keeps the test fast (zero-delay retry)
        var fc = ParseFc("""{"G": [{"p": 99.0, "b": "r", "ra": 0}]}""");

        var ex = await Assert.ThrowsAsync<TransactionOperationFailedException>(
            () => ForwardCompatibility.Check(null, ForwardCompatibility.Gets, fc));

        Assert.True(ex.RetryTransaction);
    }

    [Fact]
    public async Task Check_UnsupportedExtension_ThrowsOperationFailed()
    {
        var fc = ParseFc("""{"G": [{"e": "UNKNOWN_EXT_XYZ", "b": "f"}]}""");

        var ex = await Assert.ThrowsAsync<TransactionOperationFailedException>(
            () => ForwardCompatibility.Check(null, ForwardCompatibility.Gets, fc));

        Assert.False(ex.RetryTransaction);
    }

    [Fact]
    public async Task Check_SupportedExtension_DoesNotThrow()
    {
        // "BS" = ExtBinarySupport, listed in ProtocolVersion.ExtensionsSupported()
        var fc = ParseFc("""{"G": [{"e": "BS"}]}""");
        await ForwardCompatibility.Check(null, ForwardCompatibility.Gets, fc);
    }
}
