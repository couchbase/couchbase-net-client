using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.Exceptions.Analytics;
using Couchbase.Core.Exceptions.Search;
using Couchbase.Core.Exceptions.View;
using Couchbase.Analytics;
using Couchbase.Views;
using Couchbase.Core.Retry.Search;
using Couchbase.Query;
using Couchbase.Search;
using Couchbase.UnitTests.Helpers;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Exceptions;
using Couchbase.Management.Collections;
using Couchbase.UnitTests.Utils;
using Microsoft.Extensions.Logging;
using Moq;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using System.Text.Json;
using Xunit;

namespace Couchbase.UnitTests.Core.Exceptions
{
    /// <summary>
    /// Error contexts are the diagnostic payload attached to every exception the SDK throws, and
    /// they must honour the configured <see cref="RedactionLevel"/> the same way the log stream
    /// does. See NCBC-4296.
    /// </summary>
    public class ErrorContextRedactionTests
    {
        private static KeyValueErrorContext CreateContext(RedactionLevel level)
        {
            var op = new Get<string>
            {
                Key = "doc-key-1",
                SName = "scope1",
                CName = "coll1",
            };

            var ex = ResponseStatus.KeyNotFound.CreateException(op, "bucket1",
                new TypedRedactor(level));

            return Assert.IsType<KeyValueErrorContext>(
                Assert.IsAssignableFrom<CouchbaseException>(ex).Context);
        }

        [Fact]
        public void None_LeavesEveryFieldUntouched()
        {
            var ctx = CreateContext(RedactionLevel.None);

            Assert.Equal("doc-key-1", ctx.DocumentKey);
            Assert.Equal("bucket1", ctx.BucketName);
            Assert.Equal("scope1", ctx.ScopeName);
            Assert.Equal("coll1", ctx.CollectionName);
        }

        [Fact]
        public void Partial_RedactsUserDataOnly()
        {
            var ctx = CreateContext(RedactionLevel.Partial);

            // The document key is user data, so it is tagged even at Partial.
            Assert.Equal("<ud>doc-key-1</ud>", ctx.DocumentKey);

            // Bucket, scope and collection are metadata, which Partial deliberately leaves alone.
            Assert.Equal("bucket1", ctx.BucketName);
            Assert.Equal("scope1", ctx.ScopeName);
            Assert.Equal("coll1", ctx.CollectionName);
        }

        [Fact]
        public void Full_RedactsMetadataAsWell()
        {
            var ctx = CreateContext(RedactionLevel.Full);

            Assert.Equal("<ud>doc-key-1</ud>", ctx.DocumentKey);
            Assert.Equal("<md>bucket1</md>", ctx.BucketName);
            Assert.Equal("<md>scope1</md>", ctx.ScopeName);
            Assert.Equal("<md>coll1</md>", ctx.CollectionName);
        }

        [Fact]
        public void RedactedContext_TagsSurviveToString()
        {
            // ToString() is what lands in a log or an exception dump, and cblogredaction finds the
            // tags textually - so they must appear literally, not unicode-escaped. Assert on the
            // raw string rather than a parsed value, because the escaping is exactly what would
            // break the tooling while still round-tripping through a JSON parser.
            var json = CreateContext(RedactionLevel.Partial).ToString();

            Assert.Contains("<ud>doc-key-1</ud>", json);
            Assert.DoesNotContain(@"\u003C", json);
            Assert.DoesNotContain("\"doc-key-1\"", json);
        }

        [Theory]
        [InlineData(RedactionLevel.None)]
        [InlineData(RedactionLevel.Partial)]
        [InlineData(RedactionLevel.Full)]
        public void AbsentFields_StayNullRatherThanBecomingEmpty(RedactionLevel level)
        {
            // Redacted<T>.ToString() renders a null value as "", which would turn an absent
            // context field into a present but empty one at every redaction level.
            var op = new Get<string> { Key = "doc-key-1" };

            var ex = ResponseStatus.KeyNotFound.CreateException(op, "bucket1",
                new TypedRedactor(level));
            var ctx = (KeyValueErrorContext)((CouchbaseException)ex).Context;

            Assert.Null(ctx.ScopeName);
            Assert.Null(ctx.CollectionName);
            Assert.Null(ctx.DispatchedTo);
            Assert.Null(ctx.DispatchedFrom);
        }
    }

    /// <summary>
    /// The KV path is covered above by exercising CreateException directly. These drive the HTTP
    /// service clients and a manager so that every error-context type has at least one site
    /// pinned - without them, a future edit to any of the ~30 assignment sites drops redaction
    /// with green tests.
    /// </summary>
    public class ErrorContextRedactionClientTests
    {
        private static Queue<Task<HttpResponseMessage>> Responses(byte[] content, HttpStatusCode status)
        {
            var responses = new Queue<Task<HttpResponseMessage>>();
            for (var i = 0; i < 20; i++)
            {
                responses.Enqueue(Task.FromResult(new HttpResponseMessage
                {
                    StatusCode = status,
                    Content = new ByteArrayContent(content)
                }));
            }

            return responses;
        }

        private static byte[] Fixture(string path)
        {
            using var stream = ResourceHelper.ReadResourceAsStream(path);
            var buffer = new byte[stream.Length];
            var read = 0;
            while (read < buffer.Length)
            {
                var n = stream.Read(buffer, read, buffer.Length - read);
                if (n == 0) break;
                read += n;
            }

            return buffer;
        }

        [Fact]
        public async Task QueryErrorContext_RedactsStatement()
        {
            var client = MockedHttpClients.QueryClient(
                Responses(Fixture(@"Documents\Query\Retrys\5000.json"), HttpStatusCode.BadRequest),
                false, TestRedactor.Partial);

            // A query error surfaces when the rows are enumerated, not from QueryAsync itself,
            // so the context comes from QueryClient's ErrorContextFactory rather than one of the
            // catch blocks.
            var ex = await Assert.ThrowsAnyAsync<CouchbaseException>(async () =>
            {
                var result = await client.QueryAsync<dynamic>("SELECT * FROM `secret-bucket`",
                    new QueryOptions());
                await foreach (var _ in result) { }
            });

            var ctx = Assert.IsType<QueryErrorContext>(ex.Context);
            Assert.Equal("<ud>SELECT * FROM `secret-bucket`</ud>", ctx.Statement);
        }

        [Fact]
        public async Task SearchErrorContext_RedactsQuery()
        {
            var client = MockedHttpClients.SearchClient(
                Responses(Fixture(@"Documents\Search\query-error-400.json"), HttpStatusCode.BadRequest),
                TestRedactor.Partial);

            var request = new FtsSearchRequest
            {
                Timeout = TimeSpan.FromSeconds(1),
                Options = new SearchOptions(),
                Index = "index1"
            };

            var ex = await Assert.ThrowsAnyAsync<CouchbaseException>(() =>
                client.QueryAsync("index1", request, null, null, default));

            var ctx = Assert.IsType<SearchErrorContext>(ex.Context);

            // The serialized search query is user data, so it is tagged at Partial.
            Assert.StartsWith("<ud>", ctx.Query);

            // The index name is metadata, which Partial leaves alone.
            Assert.Equal("index1", ctx.IndexName);
        }

        [Fact]
        public async Task AnalyticsErrorContext_RedactsStatement()
        {
            var client = MockedHttpClients.AnalyticsClient(
                Responses(Fixture(@"Documents\\Analytics\\syntax-24000.json"), HttpStatusCode.BadRequest),
                TestRedactor.Partial);

            var ex = await Assert.ThrowsAnyAsync<CouchbaseException>(() =>
                client.QueryAsync<dynamic>("SELECT * FROM `secret-bucket`", new AnalyticsOptions()));

            var ctx = Assert.IsType<AnalyticsErrorContext>(ex.Context);
            Assert.Equal("<ud>SELECT * FROM `secret-bucket`</ud>", ctx.Statement);
        }

        [Fact]
        public async Task ViewContextError_RedactsDesignDocAndViewNames()
        {
            var client = MockedHttpClients.ViewClient(
                Responses(Fixture(@"Documents\\Views\\404-designdoc-notfound.json"), HttpStatusCode.NotFound),
                TestRedactor.Full);

#pragma warning disable CS0618 // Type or member is obsolete
            var query = new ViewQuery("default", "beers", "brewery_beers")
#pragma warning restore CS0618 // Type or member is obsolete
            {
                Timeout = TimeSpan.FromSeconds(1)
            };

            var ex = await Assert.ThrowsAnyAsync<CouchbaseException>(() =>
                client.ExecuteAsync<dynamic, dynamic>(query));

            var ctx = Assert.IsType<ViewContextError>(ex.Context);

            // Design doc and view names are metadata, so they are tagged only at Full.
            Assert.Equal("<md>beers</md>", ctx.DesignDocumentName);
            Assert.Equal("<md>brewery_beers</md>", ctx.ViewName);
        }

        [Fact]
        public async Task ManagementErrorContext_RedactsTheManagementUri()
        {
            using var handler = FakeHttpMessageHandler.Create(_ => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent("boom")
            });

            var baseUri = new Uri("http://localhost:8091/");
            var nodeAdapterMock = new Mock<NodeAdapter>();
            nodeAdapterMock.Object.CanonicalHostname = "localhost";

            var nodeMock = new Mock<IClusterNode>();
            nodeMock.Setup(n => n.ManagementUri).Returns(baseUri);
            nodeMock.Setup(n => n.NodesAdapter).Returns(nodeAdapterMock.Object);
            var uriProvider = new Mock<IServiceUriProvider>();
            uriProvider.Setup(x => x.GetRandomManagementUri()).Returns(baseUri);
            uriProvider.Setup(x => x.GetRandomManagementNode()).Returns(nodeMock.Object);

            var manager = new CollectionManager("default", new Mock<BucketConfig>().Object,
                uriProvider.Object, new MockHttpClientFactory(new HttpClient(handler)),
                new Mock<ILogger<CollectionManager>>().Object, TestRedactor.Full);

            var ex = await Assert.ThrowsAnyAsync<CouchbaseException>(() =>
                manager.CreateScopeAsync("scope1"));

            var ctx = Assert.IsType<ManagementErrorContext>(ex.Context);

            // The management URI is an endpoint, so it is system data and tagged at Full.
            Assert.StartsWith("<sd>", ctx.Statement);
            Assert.Contains(baseUri.Host, ctx.Statement);
        }
    }
}
