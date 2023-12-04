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

    public override string ToString()
    {
        return JsonSerializer.Serialize(Fields);
    }
}
