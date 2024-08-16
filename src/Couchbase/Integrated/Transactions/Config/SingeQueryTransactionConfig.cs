#nullable enable
using Couchbase.Core.Compatibility;
using Couchbase.KeyValue;

namespace Couchbase.Integrated.Transactions.Config;

[InterfaceStability(Level.Volatile)]
public record SingeQueryTransactionConfig(
        DurabilityLevel? DurabilityLevel = null
    );
