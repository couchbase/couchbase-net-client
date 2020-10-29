using System;

namespace Couchbase.Management.Buckets
{
    public class BucketSettings
    {
        public string Name { get; set; }
        public BucketType BucketType { get; set; } = BucketType.Couchbase;
        public int RamQuotaMB { get; set; }
        public bool FlushEnabled { get; set; }
        public int NumReplicas { get; set; }
        public bool ReplicaIndexes { get; set; }
        public ConflictResolutionType? ConflictResolutionType { get; set; }
        public EvictionPolicyType? EvictionPolicy { get; set; }
        [Obsolete("Use EvictionPolicy instead.")]
        public EvictionPolicyType? EjectionMethod
        {
            get => EvictionPolicy;
            set => EvictionPolicy = value;
        }
        public int MaxTtl { get; set; }
        public CompressionMode? CompressionMode { get; set; }
    }
}
