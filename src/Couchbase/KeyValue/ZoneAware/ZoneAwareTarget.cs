using Couchbase.Core.Sharding;

namespace Couchbase.KeyValue.ZoneAware;

/// <summary>
/// A zone-aware read target resolved ahead of the operation, so the key mapping and the
/// server group lookup are done once.
/// </summary>
internal readonly record struct ZoneAwareTarget(VBucket VBucket, int[] IndexesInGroup);
