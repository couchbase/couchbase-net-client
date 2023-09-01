using System;
using System.Diagnostics.CodeAnalysis;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Search;
using Newtonsoft.Json;

namespace Couchbase.Core.Retry.Search
{
    internal class SearchRequest : RequestBase
    {
        public override bool Idempotent => true;
        public string Index { get; set; }
        public ISearchQuery Query { get; set; }
        public SearchOptions Options { get; set; } = new();

        [RequiresUnreferencedCode(SearchClient.SearchRequiresUnreferencedMembersWarning)]
        [RequiresDynamicCode(SearchClient.SearchRequiresDynamicCodeWarning)]
        public string ToJson()
        {
            var json = Options.ToJson(Index);
            if (Query != null)
            {
                json.Add("query", Query.Export());
            }

            return json.ToString(Formatting.None);
        }

        public sealed override void StopRecording()
        {
            if (Stopwatch != null)
            {
                Stopwatch.Stop();
                MetricTracker.Search.TrackOperation(Stopwatch.Elapsed);
            }
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
