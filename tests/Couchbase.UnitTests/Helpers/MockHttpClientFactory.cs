using System;
using System.Net.Http;
using Couchbase.Core.IO.HTTP;

namespace Couchbase.UnitTests.Helpers
{
    public class MockHttpClientFactory : ICouchbaseHttpClientFactory
    {
        private readonly Func<HttpClient> _httpClientFactory;

        public MockHttpClientFactory(HttpClient httpClient)
            : this(() => httpClient)
        {
        }

        public MockHttpClientFactory(Func<HttpClient> httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public HttpClient Create() => _httpClientFactory();
        public HttpCompletionOption DefaultCompletionOption => HttpCompletionOption.ResponseContentRead;
    }
}
