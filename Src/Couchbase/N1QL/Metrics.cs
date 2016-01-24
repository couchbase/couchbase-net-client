
using System.Runtime.Serialization;

namespace Couchbase.N1QL
{
    public class Metrics
    {
        [DataMember(Name = "elapsedTime")]
        public string ElaspedTime { get; set; }

        [DataMember(Name = "executionTime")]
        public string ExecutionTime { get; set; }

        [DataMember(Name = "resultCount")]
        public uint ResultCount { get; set; }

        [DataMember(Name = "resultSize")]
        public uint ResultSize { get; set; }

        [DataMember(Name = "mutationCount")]
        public uint MutationCount { get; set; }

        [DataMember(Name = "errorCount")]
        public uint ErrorCount { get; set; }

        [DataMember(Name = "warningCount")]
        public uint WarningCount { get; set; }
    }
}
