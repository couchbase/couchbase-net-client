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


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
