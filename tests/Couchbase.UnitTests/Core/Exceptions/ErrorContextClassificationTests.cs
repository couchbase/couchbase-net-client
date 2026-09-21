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
using Couchbase.Management.Eventing;
using Couchbase.Query;
using Couchbase.Search;
using Couchbase.Stellar.Core.Retry;
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
                new Redactor(RedactionLevel.Full));

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

        private static Task<IErrorContext> Management() => ManagementWithBody("boom");

        /// <summary>
        /// The management path, with the response body under the test's control - it lands in
        /// <c>ManagementErrorContext.Message</c> raw, which is what makes its escaping matter.
        /// </summary>
        public static async Task<IErrorContext> ManagementWithBody(string body)
        {
            using var handler = FakeHttpMessageHandler.Create(_ => new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Content = new StringContent(body)
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
        /// <para>
        /// <c>ManagementErrorContext.Statement</c> is the one entry that puts a single
        /// classification on a composite value: it holds a management URI, whose host is system
        /// data while the bucket, scope, collection and index names in its path are metadata.
        /// Calling the whole thing system data costs nothing today, because metadata and system
        /// data are both redacted at Full and neither at Partial. It would start costing
        /// something the day a management URI carries user data - a document key, a user name -
        /// since the URI would then stay raw at Partial. No test can see that coming, so it is
        /// written down here instead.
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

        /// <summary>
        /// The error-context types deliberately left out of <see cref="Classification"/>, the
        /// reason each needs nothing, and the string fields that reason was checked against. A
        /// type belongs here only once someone has looked at it: the scan below fails for any
        /// context type that is neither classified nor listed here, so that a new one cannot
        /// arrive unnoticed, and <see cref="ExemptTypesStillHaveNothingToClassify"/> fails if one
        /// of them later grows a field its reason never covered.
        /// </summary>
        private static readonly Dictionary<Type, (string Reason, string[] StringFields)> NeedsNothing =
            new()
            {
                [typeof(EventingFunctionErrorContext)] = (
                    "never constructed anywhere in the SDK. Message is its only string field and " +
                    "is raw by policy; Info is [JsonIgnore(Always)] so it is never rendered.",
                    new[] { "Message" }),
                [typeof(GenericErrorContext)] = (
                    "the couchbase2:// path, which holds its fields in an untyped bag and so needs " +
                    "per-key classification rather than per-property. NCBC-4300.",
                    new[] { "Message" }),
            };

        public static IEnumerable<object[]> ExemptContextTypes =>
            NeedsNothing.Keys.Select(t => new object[] { t });

        public static IEnumerable<object[]> ContextTypes =>
            Classification.Keys.Select(t => new object[] { t });

        /// <summary>
        /// <see cref="EveryStringFieldIsClassified"/> fails closed for a new field on a known
        /// context type, but only the types listed above are checked at all - so a whole new
        /// context type would slip past it. This finds those.
        /// </summary>
        [Fact]
        public void EveryErrorContextTypeIsAccountedFor()
        {
            var contextTypes = SdkTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IErrorContext).IsAssignableFrom(t))
                .ToList();

            var unaccounted = contextTypes
                .Where(t => !Classification.ContainsKey(t) && !NeedsNothing.ContainsKey(t))
                .Select(t => t.FullName)
                .OrderBy(x => x)
                .ToList();

            Assert.True(unaccounted.Count == 0,
                $"Error-context type(s) nobody has classified: {string.Join(", ", unaccounted)}. " +
                "Either classify every string field on it and add it to the Classification table, " +
                "with a driver in ErrorContextDrivers that builds it through its real construction " +
                "path, or add it to NeedsNothing with the reason it carries nothing to redact.");

            var stale = NeedsNothing.Keys.Concat(Classification.Keys)
                .Except(contextTypes)
                .Select(t => t.FullName)
                .OrderBy(x => x)
                .ToList();

            Assert.True(stale.Count == 0,
                $"No longer an error-context type: {string.Join(", ", stale)}. Remove it from the table.");
        }

        /// <summary>
        /// A type in <see cref="NeedsNothing"/> is exempt from
        /// <see cref="EveryStringFieldIsClassified"/> for good, so without this a field added to
        /// one of them later would arrive with nobody classifying it - the exact failure the
        /// table exists to prevent, let back in through the escape hatch. Pinning the fields each
        /// reason was written against sends whoever adds one back to the reason.
        /// </summary>
        [Theory]
        [MemberData(nameof(ExemptContextTypes))]
        public void ExemptTypesStillHaveNothingToClassify(Type contextType)
        {
            var (reason, pinned) = NeedsNothing[contextType];

            var actual = StringFields(contextType).OrderBy(x => x).ToList();
            var expected = pinned.OrderBy(x => x).ToList();

            Assert.True(actual.SequenceEqual(expected),
                $"{contextType.Name}'s string fields are now {string.Join(", ", actual)}, not " +
                $"{string.Join(", ", expected)}. It is exempt from classification because it is " +
                $"{reason} Check whether the new shape still needs nothing: if it does, update the " +
                "pinned list here; if it does not, move the type into the Classification table and " +
                "give it a driver in ErrorContextDrivers.");
        }

        [Theory]
        [MemberData(nameof(ContextTypes))]
        public void EveryStringFieldIsClassified(Type contextType)
        {
            var actual = new HashSet<string>(StringFields(contextType));

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
        /// The context types that render themselves, discovered rather than listed.
        /// <c>QueryErrorContext</c> and <c>AnalyticsErrorContext</c> are absent today because
        /// neither overrides <c>ToString()</c>; NCBC-4297(c) is expected to give both one, and
        /// they are covered here the day it does.
        /// </summary>
        public static IEnumerable<object[]> ContextTypesThatRender =>
            Classification.Keys.Where(RendersItself).Select(t => new object[] { t });

        /// <summary>
        /// The tags have to reach the rendered JSON literally. cblogredaction matches them
        /// textually, so a '&lt;' serialized as <c>\u003C</c> still round-trips through a JSON
        /// parser while silently defeating the redaction pass - the failure that looks like
        /// success.
        /// <para>
        /// Each context routes its own <c>ToString()</c> through
        /// <see cref="RedactionSafeJson.RestoreTags"/>, so one context passing says nothing about
        /// the next: a context that serializes without it keeps its tags escaped. Rendering every
        /// context that has a <c>ToString()</c> is what catches the one that was forgotten.
        /// </para>
        /// </summary>
        [Theory]
        [MemberData(nameof(ContextTypesThatRender))]
        public async Task EveryContextThatRendersEmitsLiteralTags(Type contextType)
        {
            var ctx = await ErrorContextDrivers.BuildAtFull(contextType);

            var json = ctx.ToString();

            var tags = Populated(ctx, contextType, Classification[contextType].Redacted)
                .Select(f => Regex.Match(f.Value, @"^<(ud|md|sd)>"))
                .Where(m => m.Success)
                .Select(m => m.Value)
                .Distinct()
                .ToList();

            // Without this the loop below is vacuous: a driver that tags nothing would pass
            // whatever the encoder did.
            Assert.True(tags.Count > 0,
                $"The {contextType.Name} driver produced no tagged field, so this says nothing about " +
                "how its tags render. Populate a field that gets tagged at Full, or the escaping " +
                "of this context is untested.");

            foreach (var tag in tags)
            {
                Assert.True(json.Contains(tag),
                    $"{contextType.Name}.ToString() holds a {tag} field but the rendered JSON has no " +
                    $"literal '{tag}'. Route its serialization through RedactionSafeJson.RestoreTags " +
                    $"- the default encoder escapes the tags. Rendered: {json}");
            }

            foreach (var escaped in new[] { @"\u003C", @"\u003E" })
            {
                Assert.True(json.IndexOf(escaped, StringComparison.OrdinalIgnoreCase) < 0,
                    $"{contextType.Name}.ToString() emitted '{escaped}' rather than a literal angle " +
                    $"bracket, so cblogredaction cannot match its tags. Rendered: {json}");
            }
        }

        /// <summary>
        /// Restoring the tags must not unescape anything else. The obvious implementation - the
        /// relaxed JSON encoder - unescapes every value in the document, and the contexts carry
        /// content the SDK did not author and does not redact at any level:
        /// <c>ManagementErrorContext.Message</c> is the management endpoint's response body, which
        /// ns_server can return as HTML. An application that renders a context into a diagnostics
        /// page would gain an XSS sink from it, whether or not redaction was ever enabled.
        /// <para>
        /// This drives the one context whose message is a raw server body, and asserts the two
        /// halves together: the tag comes back literal, and markup in the same document does not.
        /// A relaxed encoder passes the first half and fails the second.
        /// </para>
        /// </summary>
        [Fact]
        public async Task RestoringTagsLeavesServerContentEscaped()
        {
            var ctx = await ErrorContextDrivers.ManagementWithBody("</script><script>alert(1)</script>");

            var json = ctx.ToString();

            Assert.Contains("<sd>", json);
            Assert.DoesNotContain("<script>", json);
            Assert.Contains(@"\u003Cscript\u003E", json);
        }

        /// <summary>
        /// Whether the type declares its own <c>ToString()</c> rather than inheriting object's.
        /// </summary>
        private static bool RendersItself(Type contextType) =>
            contextType.GetMethod(nameof(ToString), Type.EmptyTypes)!.DeclaringType == contextType;

        /// <summary>
        /// The public instance string properties of a context type - what has to be classified.
        /// </summary>
        private static IEnumerable<string> StringFields(Type contextType) =>
            contextType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(string))
                .Select(p => p.Name);

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

        /// <summary>
        /// Every type in the SDK assembly. Tolerates a type that will not load - on net48 the
        /// assembly is the netstandard2.0 build and a single unresolvable dependency would
        /// otherwise turn this test into a scan failure rather than a classification failure.
        /// </summary>
        private static IEnumerable<Type> SdkTypes()
        {
            try
            {
                return typeof(CouchbaseException).Assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
        }

        private static bool IsTagged(string value) =>
            Regex.IsMatch(value, @"^<(ud|md|sd)>[\s\S]*</\1>$");
    }
}
