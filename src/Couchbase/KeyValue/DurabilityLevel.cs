using System.ComponentModel;

namespace Couchbase.KeyValue
{
    /// <summary>
    /// The required number of nodes which the mutation must be replicated to (and/or persisted to) for durability requirements to be met. Possible values:
    /// </summary>
    public enum DurabilityLevel : byte
    {
        /// <summary>
        /// No durability requirements.
        /// </summary>
        [Description("none")]
        None = 0x00,

        /// <summary>
        /// Mutation must be replicated to (i.e. held in memory of that node) a majority of the configured nodes of the bucket.
        /// </summary>
        [Description("majority")]
        Majority = 0x01,

        /// <summary>
        /// Same as majority, but additionally persisted to the active node.
        /// </summary>
        [Description("majorityAndPersistActive")]
        MajorityAndPersistToActive = 0x02,

        /// <summary>
        /// Mutation must be persisted to (i.e. written and fsync'd to disk) a majority of the configured nodes of the bucket.
        /// </summary>
        [Description("persistToMajority")]
        PersistToMajority = 0x03
    }
}
