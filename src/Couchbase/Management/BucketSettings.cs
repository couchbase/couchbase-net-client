namespace Couchbase.Management
{
    public class BucketSettings
    {
        public string Name { get; set; }
        public BucketType BucketType { get; set; } = BucketType.Couchbase;
        public int RamQuota { get; set; }
        public bool FlushEnabled { get; set; }
        public int ReplicaCount { get; set; }
        public bool ReplicaIndexes { get; set; }
        public AuthType AuthType { get; set; } = AuthType.Sasl;
        public string Password { get; set; }
        public ConflictResolutionType? ConflictResolutionType { get; set; }
        public EvictionPolicyType? EvictionPolicyType { get; set; }
        public int MaxTtl { get; set; }
        public CompressionMode? CompressionMode { get; set; }
        public int ProxyPort { get; set; }
    }
}