using System;

namespace Couchbase.Services.KeyValue
{
    public class DurabilityRequirement
    {
        public DurabilityLevel Level { get; set; }

        public TimeSpan Timeout { get; set; }
    }
}
