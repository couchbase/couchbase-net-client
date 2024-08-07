#nullable enable
using System;
using System.Collections.Generic;
using Couchbase.Core.Compatibility;

namespace Couchbase.Integrated.Transactions.Config;

[InterfaceStability(Level.Volatile)]
public record TransactionCleanupConfig(
    TimeSpan? CleanupWindow = null,
    bool CleanupClientAttempts = true,
    bool CleanupLostAttempts = true,
    IEnumerable<KeySpace>? Collections = null);
