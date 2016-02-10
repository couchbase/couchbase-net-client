using Couchbase.Core.Buckets;

namespace Couchbase
{
    /// <summary>
    /// Base interface for a document.
    /// </summary>
    public interface IDocument
    {
        /// <summary>
        /// The unique identifier for the document
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// The "Check and Set" value for enforcing optimistic concurrency
        /// </summary>
        ulong Cas { get; set; }

        /// <summary>
        /// The time-to-live or TTL for the document before it's evicted from disk in milliseconds.
        /// </summary>
        /// <remarks>Setting this to zero or less will give the document infinite lifetime</remarks>
        uint Expiry { get; set; }

        /// <summary>
        /// Gets the mutation token for the operation if enhanced durability is enabled.
        /// </summary>
        /// <value>
        /// The mutation token.
        /// </value>
        /// <remarks>Note: this is used internally for enhanced durability if supported by
        /// the Couchbase server version and enabled by configuration.</remarks>
        MutationToken Token { get; }
    }
}