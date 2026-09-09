using System;
using System.Collections.Generic;
using System.Net;
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
using Couchbase.Core.Exceptions;
using Couchbase.UnitTests.Utils;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Authentication;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using static Couchbase.UnitTests.Utils.HttpFixtures;
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
    /// SelectBucket is the only operation whose Key is the bucket name rather than a document key,
    /// so it must be tagged as metadata everywhere the key is redacted. The rule lives in
    /// <c>RedactorExtensions.OperationKey</c>; these pin the two shapes it is consumed in, because
    /// the classification is invisible at the call site and silently reverts if the helper is
    /// bypassed.
    /// </summary>
    public class OperationKeyRedactionTests
    {
        [Fact]
        public void SelectBucketKey_IsMetadataInTheErrorContext()
        {
            var op = new SelectBucket { Key = "bucket1" };

            var ex = ResponseStatus.BucketNotConnected.CreateException(op, "bucket1",
                new TypedRedactor(RedactionLevel.Partial));
            var ctx = (KeyValueErrorContext)((CouchbaseException)ex).Context;

            // Metadata is left alone at Partial. As user data the bucket name would be stripped,
            // losing a diagnostic Couchbase treats as safe at that level.
            Assert.Equal("bucket1", ctx.DocumentKey);
            Assert.Equal("<md>bucket1</md>",
                new TypedRedactor(RedactionLevel.Full).OperationKeyString(op));
        }

        [Fact]
        public void DocumentKey_IsStillUserDataInTheErrorContext()
        {
            // The counterpart to the above: the carve-out must not leak to ordinary operations.
            var op = new Get<string> { Key = "doc-key-1" };

            var ex = ResponseStatus.KeyNotFound.CreateException(op, "bucket1",
                new TypedRedactor(RedactionLevel.Partial));
            var ctx = (KeyValueErrorContext)((CouchbaseException)ex).Context;

            Assert.Equal("<ud>doc-key-1</ud>", ctx.DocumentKey);
        }

        [Theory]
        [InlineData(RedactionLevel.Partial)]
        [InlineData(RedactionLevel.Full)]
        public void SelectBucketKey_IsMetadataInTheTimeoutMessage(RedactionLevel level)
        {
            // The timeout message carries the key too, and it lands in the same log as the context.
            // Classifying the same value differently in the two places would have log redaction
            // strip it from one and not the other.
            var op = new SelectBucket { Key = "bucket1" };

            var ex = ThrowHelper.CreateTimeoutException(op, new OperationCanceledException(),
                new TypedRedactor(level));

            Assert.DoesNotContain("<ud>", ex.Message);
            Assert.Contains(level == RedactionLevel.Full ? "<md>bucket1</md>" : "bucket1", ex.Message);
        }

        [Fact]
        public void DocumentKey_IsStillUserDataInTheTimeoutMessage()
        {
            var op = new Get<string> { Key = "doc-key-1" };

            var ex = ThrowHelper.CreateTimeoutException(op, new OperationCanceledException(),
                new TypedRedactor(RedactionLevel.Partial));

            Assert.Contains("<ud>doc-key-1</ud>", ex.Message);
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
            var ctx = Assert.IsType<ManagementErrorContext>(
                await ErrorContextDrivers.BuildAtFull(typeof(ManagementErrorContext)));

            // The management URI is an endpoint, so it is system data and tagged at Full.
            Assert.StartsWith("<sd>", ctx.Statement);
            Assert.Contains("localhost", ctx.Statement);
        }
    }
}
