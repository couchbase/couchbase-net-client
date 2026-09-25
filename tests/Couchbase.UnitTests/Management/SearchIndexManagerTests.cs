using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Logging;
using Couchbase.Management.Search;
using Couchbase.UnitTests.Helpers;
using Couchbase.UnitTests.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Management
{
    public class SearchIndexManagerTests
    {
        [Theory]
        [InlineData("""{"status":"ok","indexDefs":null}""")]
        [InlineData("""{"status":"ok","indexDefs":{"indexDefs":null}}""")]
        [InlineData("""{"status":"ok","indexDefs":{"uuid":"abc","implVersion":"5.7.0"}}""")]
        [InlineData("""{"status":"ok"}""")]
        public async Task GetAllIndexesAsync_NoIndexes_ReturnsEmpty(string responseBody)
        {
            var manager = CreateManager(responseBody);

            var indexes = await manager.GetAllIndexesAsync();

            Assert.Empty(indexes);
        }

        [Fact]
        public async Task GetAllIndexesAsync_OneIndex_ReturnsIndex()
        {
            const string responseBody = """
                {
                  "status": "ok",
                  "indexDefs": {
                    "uuid": "abc",
                    "indexDefs": {
                      "idx1": {
                        "type": "fulltext-index",
                        "name": "idx1",
                        "sourceType": "couchbase",
                        "sourceName": "travel-sample"
                      }
                    },
                    "implVersion": "5.7.0"
                  }
                }
                """;
            var manager = CreateManager(responseBody);

            var indexes = await manager.GetAllIndexesAsync();

            var index = Assert.Single(indexes);
            Assert.Equal("idx1", index.Name);
        }

        private static SearchIndexManager CreateManager(string responseBody)
        {
            var handler = FakeHttpMessageHandler.Create(_ => new HttpResponseMessage
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
            var httpClientFactory = new MockHttpClientFactory(new HttpClient(handler));

            var nodeMock = new Mock<IClusterNode>();
            nodeMock.Setup(n => n.SearchUri).Returns(new Uri("http://localhost:8094"));

            var serviceUriProvider = new Mock<IServiceUriProvider>();
            serviceUriProvider.Setup(m => m.GetRandomSearchNode()).Returns(nodeMock.Object);

            var context = new ClusterContext(null, new ClusterOptions().WithPasswordAuthentication("username", "password"));

            return new SearchIndexManager(serviceUriProvider.Object, httpClientFactory,
                new Mock<ILogger<SearchIndexManager>>().Object, new Mock<IRedactor>().Object, context);
        }
    }
}
