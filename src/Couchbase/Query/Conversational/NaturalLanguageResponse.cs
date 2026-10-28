using System;

#nullable enable

namespace Couchbase.Query.Conversational
{
    internal sealed class NaturalLanguageResponse
    {
        public string? GeneratedStatement { get; set; }

        public TokenUsage? RequestTokens { get; set; }

        public TokenUsage? ChatTokens { get; set; }

        public string? ChatId { get; set; }

        public TimeSpan? NaturalLanguageProcessingTime { get; set; }

        public bool? GeneratedStatementExecuted { get; set; }

        internal void ReadMetrics(MetricsData metrics)
        {
            // A duration that does not parse leaves the time unset and does not fail the query.
            NaturalLanguageProcessingTime = GoDuration.TryParse(metrics.NaturalLanguageProcessingTime, out var time)
                ? time
                : null;
            GeneratedStatementExecuted = metrics.GeneratedStatementExecuted;
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
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
