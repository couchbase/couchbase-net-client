using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Core;
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
            var httpClient = new HttpClient(handler);

            var mockClusterNode = new Mock<IClusterNode>();
            mockClusterNode.Setup(node => node.HasSearch).Returns(true);
            mockClusterNode.Setup(node => node.SearchUri).Returns(new Uri("http://localhost:8094"));
            mockClusterNode.Setup(x => x.EndPoint).Returns(new IPEndPoint(IPAddress.Any, 8091));

            var options = new ClusterOptions();
            var context = new ClusterContext(null, options);

            context.AddNode(mockClusterNode.Object);
            var client = new SearchClient(httpClient, new SearchDataMapper(), context);

            await client.QueryAsync(new SearchRequest{Index = indexName, Options = new SearchOptions()});
        }
    }
}
