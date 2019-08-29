namespace Couchbase.Management
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
        public EvictionPolicyType? EjectionMethod { get; set; }
        public int MaxTtl { get; set; }
        public CompressionMode? CompressionMode { get; set; }
    }
}
