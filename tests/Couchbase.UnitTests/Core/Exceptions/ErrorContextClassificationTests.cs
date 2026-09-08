using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.Analytics;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Exceptions.Query;
using Couchbase.Core.Exceptions.Search;
using Couchbase.Core.Exceptions.View;
using Couchbase.Core.IO;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry.Search;
using Couchbase.Management.Collections;
using Couchbase.Query;
using Couchbase.Search;
using Couchbase.UnitTests.Helpers;
using Couchbase.UnitTests.Utils;
using Couchbase.Views;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using static Couchbase.UnitTests.Utils.HttpFixtures;

namespace Couchbase.UnitTests.Core.Exceptions
{
    /// <summary>
    /// Shared drivers that build each error-context type by exercising the real construction path,
    /// rather than by assigning to the context directly - assigning would bypass the very
    /// redaction these tests exist to pin.
    /// </summary>
    internal static class ErrorContextDrivers
    {
        /// <summary>
        /// Builds a context of the given type with as many of its fields populated as the path
        /// allows, at <see cref="RedactionLevel.Full"/> so that every classification is visible.
        /// </summary>
        public static Task<IErrorContext> BuildAtFull(Type contextType)
        {
            if (contextType == typeof(KeyValueErrorContext)) return Task.FromResult(KeyValue());
            if (contextType == typeof(QueryErrorContext)) return Query();
            if (contextType == typeof(AnalyticsErrorContext)) return Analytics();
            if (contextType == typeof(SearchErrorContext)) return Search();
            if (contextType == typeof(ViewContextError)) return View();
            if (contextType == typeof(ManagementErrorContext)) return Management();

            throw new ArgumentOutOfRangeException(nameof(contextType), contextType,
                "No driver for this error-context type. Add one so its fields are covered.");
        }

        private static IErrorContext KeyValue()
        {
            // A mocked operation rather than a real one: DispatchedFrom/DispatchedTo are only
            // written when an operation is actually dispatched over a connection, and they are
            // two of the fields that have to come back redacted.
            var op = new Mock<IOperation>();
            op.SetupGet(x => x.Key).Returns("doc-key-1");
            op.SetupGet(x => x.SName).Returns("scope1");
            op.SetupGet(x => x.CName).Returns("coll1");
            op.SetupGet(x => x.OpCode).Returns(OpCode.Get);
            op.SetupGet(x => x.Opaque).Returns(42u);
            op.SetupGet(x => x.LastDispatchedFrom).Returns("127.0.0.1:51234");
            op.SetupGet(x => x.LastDispatchedTo).Returns("127.0.0.1:11210");

            var ex = ResponseStatus.KeyNotFound.CreateException(op.Object, "bucket1",
                new TypedRedactor(RedactionLevel.Full));

            return ((CouchbaseException)ex).Context;
        }

        private static async Task<IErrorContext> Query()
        {
            var client = MockedHttpClients.QueryClient(
                Responses(Fixture(@"Documents\Query\Retrys\5000.json"), HttpStatusCode.BadRequest),
                false, TestRedactor.Full);

            // A query error surfaces on enumeration, not from QueryAsync itself.
            return await Capture(async () =>
            {
                var result = await client.QueryAsync<dynamic>("SELECT * FROM `secret-bucket`",
                    new QueryOptions().Parameter("secret-value"));
                await foreach (var _ in result) { }
            });
        }

        private static async Task<IErrorContext> Analytics()
        {
            var client = MockedHttpClients.AnalyticsClient(
                Responses(Fixture(@"Documents\Analytics\syntax-24000.json"), HttpStatusCode.BadRequest),
                TestRedactor.Full);

            return await Capture(() => client.QueryAsync<dynamic>("SELECT * FROM `secret-bucket`",
                new AnalyticsOptions().Parameter("secret-value")));
        }

        private static async Task<IErrorContext> Search()
        {
            var client = MockedHttpClients.SearchClient(
                Responses(Fixture(@"Documents\Search\query-error-400.json"), HttpStatusCode.BadRequest),
                TestRedactor.Full);

            var request = new FtsSearchRequest
            {
                Timeout = TimeSpan.FromSeconds(1),
                Options = new SearchOptions(),
                Index = "index1"
            };

            return await Capture(() => client.QueryAsync("index1", request, null, null, default));
        }

        private static async Task<IErrorContext> View()
        {
            var client = MockedHttpClients.ViewClient(
                Responses(Fixture(@"Documents\Views\404-designdoc-notfound.json"), HttpStatusCode.NotFound),
                TestRedactor.Full);

#pragma warning disable CS0618 // Type or member is obsolete
            var query = new ViewQuery("default", "beers", "brewery_beers")
#pragma warning restore CS0618 // Type or member is obsolete
            {
                Timeout = TimeSpan.FromSeconds(1)
            };

            return await Capture(() => client.ExecuteAsync<dynamic, dynamic>(query));
        }

        private static async Task<IErrorContext> Management()
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

            return await Capture(() => manager.CreateScopeAsync("scope1"));
        }

        private static async Task<IErrorContext> Capture(Func<Task> failingCall)
        {
            var ex = await Assert.ThrowsAnyAsync<CouchbaseException>(failingCall);
            return Assert.IsAssignableFrom<IErrorContext>(ex.Context);
        }
    }

    /// <summary>
    /// Redaction is applied field by field at ~30 construction sites, so the failure mode of the
    /// design is a field that nobody classified. These tests make that decision unskippable: every
    /// string field on every error-context type has to be listed below as either redacted or
    /// deliberately raw, and the listing is then checked against what the real construction paths
    /// actually produce. See NCBC-4296.
    /// </summary>
    public class ErrorContextClassificationTests
    {
        /// <summary>
        /// The classification of every string field on every error-context type.
        /// <para>
        /// Left raw on purpose: <c>ClientContextId</c>, which support correlates against server
        /// logs, and the server-authored <c>Message</c> and <c>Errors</c> text.
        /// </para>
        /// <para>
        /// <c>Parameters</c> on the search and view contexts is listed as redacted although no
        /// construction site populates it today - it would carry user data if one ever did, and
        /// the behavioural test below skips fields a path leaves empty.
        /// </para>
        /// </summary>
        private static readonly Dictionary<Type, (string[] Redacted, string[] Raw)> Classification =
            new()
            {
                [typeof(KeyValueErrorContext)] = (
                    new[] { "DocumentKey", "BucketName", "ScopeName", "CollectionName", "DispatchedFrom", "DispatchedTo" },
                    new[] { "ClientContextId", "Message" }),
                [typeof(QueryErrorContext)] = (
                    new[] { "Statement", "Parameters" },
                    new[] { "ClientContextId", "Message" }),
                [typeof(AnalyticsErrorContext)] = (
                    new[] { "Statement", "Parameters" },
                    new[] { "ClientContextId", "Message" }),
                [typeof(SearchErrorContext)] = (
                    new[] { "IndexName", "Query", "Statement", "Parameters" },
                    new[] { "ClientContextId", "Message", "Errors" }),
                [typeof(ViewContextError)] = (
                    new[] { "DesignDocumentName", "ViewName", "Parameters" },
                    new[] { "ClientContextId", "Message", "Errors" }),
                [typeof(ManagementErrorContext)] = (
                    new[] { "Statement" },
                    new[] { "ClientContextId", "Message" }),
            };

        public static IEnumerable<object[]> ContextTypes =>
            Classification.Keys.Select(t => new object[] { t });

        [Theory]
        [MemberData(nameof(ContextTypes))]
        public void EveryStringFieldIsClassified(Type contextType)
        {
            var actual = new HashSet<string>(contextType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(string))
                .Select(p => p.Name));

            var (redacted, raw) = Classification[contextType];
            var classified = new HashSet<string>(redacted.Concat(raw));

            var unclassified = actual.Except(classified).OrderBy(x => x).ToList();
            Assert.True(unclassified.Count == 0,
                $"{contextType.Name} has unclassified string field(s): {string.Join(", ", unclassified)}. " +
                "Decide whether each one carries user data, metadata or system data - if so, redact it " +
                "at every site that assigns it and add it to the Redacted list; if it should stay raw " +
                "(a server-authored message, an id support needs to correlate) add it to the Raw list.");

            var stale = classified.Except(actual).OrderBy(x => x).ToList();
            Assert.True(stale.Count == 0,
                $"{contextType.Name} no longer has field(s): {string.Join(", ", stale)}. " +
                "Remove them from the classification table.");
        }

        [Theory]
        [MemberData(nameof(ContextTypes))]
        public async Task ClassifiedFieldsAreTaggedAsDeclared(Type contextType)
        {
            var ctx = await ErrorContextDrivers.BuildAtFull(contextType);
            var (redacted, raw) = Classification[contextType];

            foreach (var (name, value) in Populated(ctx, contextType, redacted))
            {
                Assert.True(IsTagged(value),
                    $"{contextType.Name}.{name} is classified as redacted but came back raw: '{value}'. " +
                    "A construction site is assigning it without going through the redactor.");
            }

            foreach (var (name, value) in Populated(ctx, contextType, raw))
            {
                Assert.False(IsTagged(value),
                    $"{contextType.Name}.{name} is classified as raw but came back tagged: '{value}'.");
            }
        }

        /// <summary>
        /// The named fields that this construction path actually filled in. Fields a path leaves
        /// empty say nothing either way, and are covered by
        /// <see cref="EveryStringFieldIsClassified"/> instead.
        /// </summary>
        private static IEnumerable<(string Name, string Value)> Populated(IErrorContext ctx,
            Type contextType, IEnumerable<string> names) =>
            names
                .Select(name => (Name: name,
                    Value: (string)contextType.GetProperty(name)!.GetValue(ctx)))
                .Where(f => !string.IsNullOrEmpty(f.Value));

        private static bool IsTagged(string value) =>
            Regex.IsMatch(value, @"^<(ud|md|sd)>[\s\S]*</\1>$");
    }
}
