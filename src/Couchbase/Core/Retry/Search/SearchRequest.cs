using System;
using System.Collections.Generic;
using System.Threading;
using Couchbase.Search;
using Newtonsoft.Json;

namespace Couchbase.Core.Retry.Search
{
    internal class SearchRequest : IRequest
    {
        private IRetryStrategy _retryStrategy;
        public uint Attempts { get; set; }
        public bool Idempotent { get; } = true;
        public List<RetryReason> RetryReasons { get; set; } = new List<RetryReason>();
        public IRetryStrategy RetryStrategy
        {
            get => _retryStrategy ??= new BestEffortRetryStrategy();
            set => _retryStrategy = value;
        }
        public TimeSpan Timeout { get; set; }
        public CancellationToken Token { get; set; }
        public string ClientContextId { get; set; }
        public string Statement { get; set; }

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
