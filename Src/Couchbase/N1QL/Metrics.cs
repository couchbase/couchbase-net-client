
using System.Runtime.Serialization;

namespace Couchbase.N1QL
{
    public class Metrics
    {
        public string ElaspedTime { get; set; }

        public string ExecutionTime { get; set; }

        public uint ResultCount { get; set; }

        public uint ResultSize { get; set; }

        public uint MutationCount { get; set; }

        public uint ErrorCount { get; set; }

        public uint WarningCount { get; set; }

        public uint SortCount { get; set; }
    }

    internal class MetricsData
    {
        public string elapsedTime { get; set; }
        public string executionTime { get; set; }
        public uint resultCount { get; set; }
        public uint resultSize { get; set; }
        public uint mutationCount { get; set; }
        public uint errorCount { get; set; }
        public uint warningCount { get; set; }
        public uint sortCount { get; set; }

        internal Metrics ToMetrics()
        {
            return new Metrics
            {
                ElaspedTime = elapsedTime,
                ExecutionTime = executionTime,
                ResultCount = resultCount,
                ResultSize = resultSize,
                MutationCount = mutationCount,
                ErrorCount = errorCount,
                WarningCount = warningCount,
                SortCount = sortCount,
            };
        }
    }
}
