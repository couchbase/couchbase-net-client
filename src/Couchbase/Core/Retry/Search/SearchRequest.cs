using System;
using Couchbase.Search;
using Newtonsoft.Json;

namespace Couchbase.Core.Retry.Search
{
    internal class SearchRequest : RequestBase
    {
        public override bool Idempotent => true;
        public string Index { get; set; }
        public ISearchQuery Query { get; set; }
        public SearchOptions Options { get; set; }

        public string ToJson()
        {
            var json = Options.ToJson();
            if (Query != null)
            {
                json.Add("query", Query.Export());
            }

            return json.ToString(Formatting.None);
        }
    }
}
