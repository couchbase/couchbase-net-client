#nullable enable
using Couchbase.Core.Compatibility;
using Newtonsoft.Json;
using System.Text.Json.Serialization;

// ReSharper disable InconsistentNaming

namespace Couchbase.Query
{
    /// <summary>
    /// The "cause" object returned by the query service inside a transaction-related error
    /// (e.g. a CAS mismatch at COMMIT). Carries the protocol hints the transactions layer needs
    /// to decide whether to retry/rollback and which final error to raise.
    /// </summary>
    /// <remarks>
    /// Deserialized as part of <see cref="Error"/> by whichever serializer the cluster is configured
    /// with (Newtonsoft or System.Text.Json), hence the dual attributes - matching <see cref="Error"/>
    /// and <see cref="Reason"/>.
    /// </remarks>
    [InterfaceStability(Level.Volatile)]
    public class QueryErrorCause
    {
        public QueryErrorCause(object? cause, bool? rollback, bool? retry, string? raise)
        {
            this.cause = cause;
            this.rollback = rollback;
            this.retry = retry;
            this.raise = raise;
        }

        [JsonProperty("cause")]
        [JsonPropertyName("cause")]
        public object? cause { get; }

        [JsonProperty("rollback")]
        [JsonPropertyName("rollback")]
        public bool? rollback { get; }

        [JsonProperty("retry")]
        [JsonPropertyName("retry")]
        public bool? retry { get; }

        [JsonProperty("raise")]
        [JsonPropertyName("raise")]
        public string? raise { get; }
    }
}
