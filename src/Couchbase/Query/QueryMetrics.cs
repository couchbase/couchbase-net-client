using System;

namespace Couchbase.Query
{
    public class QueryMetrics
    {
        [Obsolete("Use ElapsedTime property instead.")]
        // ReSharper disable once IdentifierTypo
        public string ElaspedTime
        {
            get => ElapsedTime;
            set => ElapsedTime = value;
        }

        public string ElapsedTime { get; set; }

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
        public string ElapsedTime { get; set; }
        public string ExecutionTime { get; set; }
        public uint ResultCount { get; set; }
        public uint ResultSize { get; set; }
        public uint MutationCount { get; set; }
        public uint ErrorCount { get; set; }
        public uint WarningCount { get; set; }
        public uint SortCount { get; set; }

        internal QueryMetrics ToMetrics()
        {
            return new()
            {
                ElapsedTime = ElapsedTime,
                ExecutionTime = ExecutionTime,
                ResultCount = ResultCount,
                ResultSize = ResultSize,
                MutationCount = MutationCount,
                ErrorCount = ErrorCount,
                WarningCount = WarningCount,
                SortCount = SortCount,
            };
        }
    }
}
