using System;

namespace Couchbase
{
    public class DurabilityRequirement
    {
        public DurabilityLevel Level { get; set; }

        public TimeSpan Timeout { get; set; }
    }
}
