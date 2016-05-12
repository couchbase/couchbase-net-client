using System.Runtime.Serialization;

namespace Couchbase.Management.Indexes
{
    /// <summary>
    /// Represents the meta-data related to an index in Couchbase Server.
    /// </summary>
    [DataContract]
    public sealed class IndexInfo
    {
        /// <summary>
        /// The name of the index.
        /// </summary>
        [DataMember(Name = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Whether or not the index is a primary index - <c>true</c> if a primary index; otherwise <c>false</c>.
        /// </summary>
        [DataMember(Name = "is_primary")]
        public bool IsPrimary { get; set; }

        /// <summary>
        /// The type of index
        /// </summary>
        [DataMember(Name = "type")]
        public IndexType Type { get; set; } //enum type GSI/VIEW

        /// <summary>
        /// Raw string type in case a new index type is introduced, for forward-compatibility
        /// </summary>
        [DataMember(Name = "raw_type")]
        public string RawType { get; set; }

        /// <summary>
        /// The indexes current state.
        /// </summary>
        [DataMember(Name = "state")]
        public string State { get; set; } //eg. "online", "pending" or "deferred"/"building"

        /// <summary>
        /// The keyspace that the index belongs to.
        /// </summary>
        [DataMember(Name = "keyspace_id")]
        public string Keyspace { get; set; }

        /// <summary>
        /// The namespace that the index belongs to.
        /// </summary>
        [DataMember(Name = "namespace_id")]
        public string Namespace { get; set; }

        /// <summary>
        /// The index key.
        /// </summary>
        [DataMember(Name = "index_key")]
        public dynamic IndexKey { get; set; }

        /// <summary>
        /// The predicate.
        /// </summary>
        [DataMember(Name="condition")]
        public string Condtion { get; set; }
    }
}
