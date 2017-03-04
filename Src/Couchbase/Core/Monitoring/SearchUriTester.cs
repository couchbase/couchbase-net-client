using System;
using System.Net.Http;
using Couchbase.Logging;
using Couchbase.N1QL;

namespace Couchbase.Core.Monitoring
{
    /// <summary>
    /// Tests a search URI that previously failed to see if it's back online again.
    /// </summary>
    internal class SearchUriTester : UriTesterBase
    {
        public SearchUriTester(HttpClient httpClient)
            : base(httpClient, LogManager.GetLogger<SearchUriTester>())
        {
        }

        protected override string NodeType
        {
            get { return "Search"; }
        }

        protected override Uri GetPingUri(FailureCountingUri uri)
        {
            return new Uri(uri, "/api/ping");
        }
    }
}
