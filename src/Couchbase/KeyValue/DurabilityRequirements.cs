using System;

namespace Couchbase.KeyValue
{
    public class DurabilityRequirement
    {
        public DurabilityLevel Level { get; set; }

        public TimeSpan Timeout { get; set; }
    }
}
