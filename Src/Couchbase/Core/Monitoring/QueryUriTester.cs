using System;
using System.Net.Http;
using Couchbase.Logging;
using Couchbase.N1QL;

namespace Couchbase.Core.Monitoring
{
    /// <summary>
    /// Tests a query URI that previously failed to see if it's back online again.
    /// </summary>
    internal class QueryUriTester : UriTesterBase
    {
        public QueryUriTester(HttpClient httpClient)
            : base(httpClient, LogManager.GetLogger<QueryUriTester>())
        {
        }

        protected override string NodeType
        {
            get { return "Query"; }
        }

        protected override Uri GetPingUri(FailureCountingUri uri)
        {
            return new Uri(uri, "/admin/ping");
        }
    }
}
