using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry.Search;
using Couchbase.Search;
using Couchbase.Search.Queries.Simple;
using Couchbase.UnitTests.Helpers;
using Couchbase.UnitTests.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Couchbase.UnitTests.Search;

public class SearchClientTests
{
    [Fact]
    public async Task Query_IndexNotFound_Throws_IndexNotFoundException()
    {
        const string indexName = "test-index";

        using var responseStream = ResourceHelper.ReadResourceAsStream("query-error-index-not-found-400.json");
        using var handler = FakeHttpMessageHandler.Create(_ => new HttpResponseMessage
        {
            // ReSharper disable once AccessToDisposedClosure
            Content = new StreamContent(responseStream),
            StatusCode = HttpStatusCode.BadRequest
        });
        var httpClient = new HttpClient(handler);
        var httpClientFactory = new MockHttpClientFactory(httpClient);

        var nodeMock = new Mock<IClusterNode>();
        nodeMock
            .Setup(n => n.SearchUri)
            .Returns(new Uri("http://localhost:8093"));

        var mockServiceUriProvider = new Mock<IServiceUriProvider>();
        mockServiceUriProvider
            .Setup(m => m.GetRandomSearchUri())
            .Returns(new Uri("http://localhost:8093"));
        mockServiceUriProvider
            .Setup(m => m.GetRandomSearchNode(It.IsAny<System.Collections.Generic.IList<System.Uri>>()))
            .Returns(nodeMock.Object);

        var client = new SearchClient(httpClientFactory, mockServiceUriProvider.Object,
            new Mock<ILogger<SearchClient>>().Object, NoopRequestTracer.Instance, new Redactor(RedactionLevel.None));

        await Assert.ThrowsAsync<IndexNotFoundException>(async () => await client.QueryAsync(indexName, new FtsSearchRequest {Index = indexName}, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task Query_200_All_TimedOut_Does_Not_Throw()
    {
        const string indexName = "test-index";

        using var responseStream = ResourceHelper.ReadResourceAsStream("alltimeouts.json");
        using var handler = FakeHttpMessageHandler.Create(_ => new HttpResponseMessage
        {
            // ReSharper disable once AccessToDisposedClosure
            Content = new StreamContent(responseStream),
            StatusCode = HttpStatusCode.OK
        });
        var httpClient = new HttpClient(handler);
        var httpClientFactory = new MockHttpClientFactory(httpClient);

        var nodeMock = new Mock<IClusterNode>();
        nodeMock
            .Setup(n => n.SearchUri)
            .Returns(new Uri("http://localhost:8093"));

        var nodeAdapterMock = new Mock<NodeAdapter>();
        nodeAdapterMock.Object.CanonicalHostname = "localhost";

        nodeMock.Setup(n => n.NodesAdapter)
            .Returns(nodeAdapterMock.Object);

        var mockServiceUriProvider = new Mock<IServiceUriProvider>();
        mockServiceUriProvider
            .Setup(m => m.GetRandomSearchUri())
            .Returns(new Uri("http://localhost:8093"));
        mockServiceUriProvider
            .Setup(m => m.GetRandomSearchNode(It.IsAny<System.Collections.Generic.IList<System.Uri>>()))
            .Returns(nodeMock.Object);

        var client = new SearchClient(httpClientFactory, mockServiceUriProvider.Object,
            new Mock<ILogger<SearchClient>>().Object, NoopRequestTracer.Instance, new Redactor(RedactionLevel.None));

        var response =  await client.QueryAsync(indexName, new FtsSearchRequest { Index = indexName }, null, null, CancellationToken.None);
        Assert.Equal(6, response.MetaData.ErrorCount);
        Assert.Equal(6, response.MetaData.TotalCount);
        Assert.Equal(0, response.MetaData.SuccessCount);
        Assert.Equal(6, response.MetaData.Errors.Count);
    }

    /// <summary>
    /// Regression coverage: the mocked <see cref="ILogger{SearchClient}"/> used by the other tests in
    /// this file has Trace disabled by default, so the redaction branch that logs the FTS request body
    /// (added alongside the SDK's log-redaction compliance work) is never exercised or inspected by
    /// them. With Trace enabled and a redacting level configured, the logged body must carry the
    /// &lt;ud&gt; tags around the actual request content, not the raw, untagged JSON.
    /// </summary>
    [Fact]
    public async Task Query_WithRedactionEnabled_And_TraceLogging_Redacts_Logged_RequestBody()
    {
        const string indexName = "test-index";
        const string secretSearchTerm = "super-secret-user-search-term";

        using var responseStream = ResourceHelper.ReadResourceAsStream("alltimeouts.json");
        using var handler = FakeHttpMessageHandler.Create(_ => new HttpResponseMessage
        {
            // ReSharper disable once AccessToDisposedClosure
            Content = new StreamContent(responseStream),
            StatusCode = HttpStatusCode.OK
        });
        var httpClient = new HttpClient(handler);
        var httpClientFactory = new MockHttpClientFactory(httpClient);

        var nodeMock = new Mock<IClusterNode>();
        nodeMock
            .Setup(n => n.SearchUri)
            .Returns(new Uri("http://localhost:8093"));

        var nodeAdapterMock = new Mock<NodeAdapter>();
        nodeAdapterMock.Object.CanonicalHostname = "localhost";

        nodeMock.Setup(n => n.NodesAdapter)
            .Returns(nodeAdapterMock.Object);

        var mockServiceUriProvider = new Mock<IServiceUriProvider>();
        mockServiceUriProvider
            .Setup(m => m.GetRandomSearchUri())
            .Returns(new Uri("http://localhost:8093"));
        mockServiceUriProvider
            .Setup(m => m.GetRandomSearchNode(It.IsAny<IList<Uri>>()))
            .Returns(nodeMock.Object);

        var loggerMock = new Mock<ILogger<SearchClient>>();
        loggerMock.Setup(l => l.IsEnabled(LogLevel.Trace)).Returns(true);

        var client = new SearchClient(httpClientFactory, mockServiceUriProvider.Object,
            loggerMock.Object, NoopRequestTracer.Instance, new Redactor(RedactionLevel.Full));

        await client.QueryAsync(indexName,
            new FtsSearchRequest { Index = indexName, Query = new MatchQuery(secretSearchTerm) },
            null, null, CancellationToken.None);

        loggerMock.Verify(l => l.Log(
                LogLevel.Trace,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state.ToString().Contains("<ud>") &&
                    state.ToString().Contains("</ud>") &&
                    state.ToString().Contains(secretSearchTerm)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            Times.Once);
    }
}

internal class FakeMessageHandler : HttpMessageHandler
{
    public HttpRequestMessage RequestMessage { get; private set; }

    public HttpStatusCode StatusCode { get; set; }

    public HttpContent Content { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
       RequestMessage = request;
      var response = new HttpResponseMessage(StatusCode);
      if (Content != null)
      {
           response.Content = Content;
       }
       return Task.FromResult(response);
   }
}

#region [License information]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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

#endregion
