using System.Collections.Generic;
using System.Text.Json;
using Couchbase.Core;
using Couchbase.Core.Retry;

#nullable enable
namespace Couchbase.Stellar.Core.Retry;

public class GenericErrorContext : IErrorContext
{
    public string? Message { get; set; }
    public List<RetryReason> RetryReasons { get; } = new();
    public Dictionary<string, object> Fields { get; } = new();

    // RestoreTags keeps the redaction tags in Fields readable by cblogredaction (NCBC-4300).
    public override string ToString()
    {
        return RedactionSafeJson.RestoreTags(JsonSerializer.Serialize(Fields));
    }
}
