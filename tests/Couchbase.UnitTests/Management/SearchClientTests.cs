using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Retry.Search;
using Couchbase.Search;
using Couchbase.UnitTests.Fixtures;
using Couchbase.UnitTests.Utils;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Management
{
    public class SearchClientTests : IClassFixture<ClusterFixture>
    {
        [Fact]
        public async Task SearchQueriesUseCorrectPath()
        {
            const string indexName = "test-index";

            using var handler = FakeHttpMessageHandler.Create((req) =>
            {
                Assert.Equal("http://localhost:8094/api/index/test-index/query", req.RequestUri.ToString());
                return new HttpResponseMessage
                {
                    Content = new StreamContent(new MemoryStream())
                };
            });
            var httpClient = new CouchbaseHttpClient(handler);

            var mockServiceUriProvider = new Mock<IServiceUriProvider>();
            mockServiceUriProvider
                .Setup(m => m.GetRandomSearchUri())
                .Returns(new Uri("http://localhost:8094"));

            var client = new SearchClient(httpClient, mockServiceUriProvider.Object, new SearchDataMapper());

            await client.QueryAsync(new SearchRequest{Index = indexName, Options = new SearchOptions()});
        }
    }
}
