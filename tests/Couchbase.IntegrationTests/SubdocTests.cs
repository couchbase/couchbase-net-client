using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Transcoders;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.Utils;
using Couchbase.KeyValue;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Couchbase.IntegrationTests
{
    public class SubdocTests : IClassFixture<ClusterFixture>
    {
        private const string DocumentKey = "document-key";
        private readonly ClusterFixture _fixture;

        public SubdocTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Can_Return_Expiry()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            await collection.UpsertAsync("Can_Return_Expiry()", new {foo = "bar", bar = "foo"}, options =>options.Expiry(TimeSpan.FromHours(1))).ConfigureAwait(false);

            var result = await collection.GetAsync("Can_Return_Expiry()", options=>options.Expiry()).ConfigureAwait(false);
            Assert.NotNull(result.ExpiryTime);
        }

        [Fact]
        public async Task LookupIn_Can_Return_FullDoc()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            await collection.UpsertAsync("LookupIn_Can_Return_FullDoc()", new {foo = "bar", bar = "foo"}, options =>options.Expiry(TimeSpan.FromHours(1))).ConfigureAwait(false);

            var result = await collection.LookupInAsync("LookupIn_Can_Return_FullDoc()", builder=>builder.GetFull());
            var doc = result.ContentAs<dynamic>(0);
            Assert.NotNull(doc);
        }

        [Fact]
        public async Task LookupIn_Xattr_In_Any_Order()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            await collection.UpsertAsync(nameof(LookupIn_Xattr_In_Any_Order), new { foo = "bar", bar = "foo" }, options => options.Expiry(TimeSpan.FromHours(1))).ConfigureAwait(false);
            var result = await collection.LookupInAsync(nameof(LookupIn_Xattr_In_Any_Order), specs =>
                specs.Get("$document", true)
                    .Get("foo", false)
                    .Get("$document.exptime", true)
            );

            var doc = result.ContentAs<dynamic>(2);
            Assert.NotNull(doc);
            var metadata = result.ContentAs<dynamic>(0);
            Assert.NotNull(metadata);
            Assert.Equal("bar", (string)result.ContentAs<string>(1));
        }

        [Fact]
        public async Task Can_perform_lookup_in()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            await collection.UpsertAsync(DocumentKey, new {foo = "bar", bar = "foo"}).ConfigureAwait(false);

            var result = await collection.LookupInAsync(DocumentKey, ops =>
            {
                ops.Get("foo");
                ops.Get("bar");
            }).ConfigureAwait(false);

            Assert.Equal("bar", result.ContentAs<string>(0));
            Assert.Equal("foo", result.ContentAs<string>(1));
        }

        [Fact]
        public async Task Can_perform_lookup_in_via_lambda()
        {
            var key = Guid.NewGuid().ToString();

            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            await collection.UpsertAsync(key, new TestDoc {Foo = "bar", Bar = "foo"}).ConfigureAwait(false);

            try
            {
                var result = await collection.LookupInAsync<TestDoc>(key, ops =>
                {
                    ops.Get(p => p.Foo);
                    ops.Get(p => p.Bar);
                }).ConfigureAwait(false);

                Assert.Equal("bar", result.ContentAs(p => p.Foo));
                Assert.Equal("foo", result.ContentAs(p => p.Bar));
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [Fact]
        public async Task Can_perform_lookup_in_with_Exists()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            await collection.UpsertAsync(DocumentKey, new { foo = "bar", bar = "foo" }).ConfigureAwait(false);

            var result = await collection.LookupInAsync(DocumentKey, ops =>
            {
                ops.Get("foo");
                ops.Exists("bwar");
            }).ConfigureAwait(false);

            Assert.True(result.Exists(0));
            Assert.Equal("bar", result.ContentAs<string>(0));
            Assert.False(result.Exists(1));
        }

        [Fact]
        public async Task Can_do_lookup_in_with_array()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            await collection.UpsertAsync(DocumentKey, new {foo = "bar", bar = "foo"}).ConfigureAwait(false);

            var result = await collection.LookupInAsync(DocumentKey, new[]
            {
                LookupInSpec.Get("foo"),
                LookupInSpec.Get("bar")
            }).ConfigureAwait(false);

            Assert.Equal("bar", result.ContentAs<string>(0));
            Assert.Equal("foo", result.ContentAs<string>(1));
        }

        [Fact]
        public async Task Can_perform_mutate_in()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            await collection.UpsertAsync(DocumentKey,  new {foo = "bar", bar = "foo"}).ConfigureAwait(false);

            await collection.MutateInAsync(DocumentKey, ops =>
            {
                ops.Upsert("name", "mike");
                ops.Replace("bar", "bar");
                ops.Insert("bah", 0);
                ops.Increment("zzz", 10, true);
                ops.Decrement("xxx", 5, true);
            }).ConfigureAwait(false);

            using (var getResult = await collection.GetAsync(DocumentKey, options=>options.Transcoder(new LegacyTranscoder())).ConfigureAwait(false))
            {
                var content = getResult.ContentAs<string>();

                var expected = new
                {
                    foo = "bar",
                    bar = "bar",
                    name = "mike",
                    bah = 0,
                    zzz = 10,
                    xxx = -5
                };
                Assert.Equal(JsonConvert.SerializeObject(expected), content);
            }
        }

        [Fact]
        public async Task Can_perform_mutate_in_via_lambda()
        {
            var key = Guid.NewGuid().ToString();

            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            await collection.UpsertAsync(key, new TestDoc {Foo = "bar", Bar = "foo"}).ConfigureAwait(false);

            try
            {
                var res = await collection.MutateInAsync<TestDoc>(key, ops =>
                {
                    ops.Upsert(p => p.Name, "mike");
                    ops.Replace(p => p.Bar, "bar");
                    ops.Insert(p => p.Bah, 0);
                    ops.Increment(p => p.Zzz, 10, true);
                    ops.Decrement(p => p.Xxx, 5, true);
                }).ConfigureAwait(false);

                using (var getResult = await collection
                    .GetAsync(key, options => options.Transcoder(new LegacyTranscoder())).ConfigureAwait(false))
                {
                    var content = getResult.ContentAs<string>();

                    var expected = new
                    {
                        foo = "bar",
                        bar = "bar",
                        name = "mike",
                        bah = 0,
                        zzz = 10,
                        xxx = -5
                    };
                    Assert.Equal(JsonConvert.SerializeObject(expected), content);
                }
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [Fact]
        public async Task MutateIn_Xattr_In_Any_Order()
        {
            var docId = nameof(MutateIn_Xattr_In_Any_Order);
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            await collection.UpsertAsync(docId, new { foo = "bar", bar = "foo", xxx = 0 }).ConfigureAwait(false);

            var mutateResult = await collection.MutateInAsync(docId, ops =>
            {
                ops.Upsert("name", "mike", true);
                ops.Upsert("txnid", "pretend_this_is_a_guid", createPath: true, isXattr: true);
                ops.Decrement("xxx", 5, true);
            },
                options => options.StoreSemantics(StoreSemantics.Upsert)).ConfigureAwait(false);

            // Upserts don't result in values from MutateIn, but Increment/Decrement do.
            Assert.Equal(-5, mutateResult.ContentAs<int>(2));

            // Attempting to get the result of an upsert/insert as a non-nullable value results in default(T)
            Assert.Equal(default(int), mutateResult.ContentAs<int>(0));

            // Attempting to get the result of an upsert/insert as a nullable type results in null.
            Assert.Null(mutateResult.ContentAs<string>(0));

            var lookupInResult = await collection.LookupInAsync(nameof(MutateIn_Xattr_In_Any_Order), specs =>
                specs.Get("xxx")
                    .Get("txnid", isXattr: true)
                    .Get("name")
            );

            Assert.Equal(-5, lookupInResult.ContentAs<int>(0));
            Assert.Equal("pretend_this_is_a_guid", (string)lookupInResult.ContentAs<string>(1));
            Assert.Equal("mike", (string)lookupInResult.ContentAs<string>(2));
        }

        [Fact]
        public async Task Can_perform_mutate_in_with_array()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            await collection.UpsertAsync(DocumentKey, new {foo = "bar", bar = "foo"}).ConfigureAwait(false);

            var result = await collection.MutateInAsync(DocumentKey, new[]
            {
                MutateInSpec.Upsert("name", "mike"),
                MutateInSpec.Replace("bar", "bar")
            }).ConfigureAwait(false);

            using var getResult = await collection.GetAsync(DocumentKey, options => options.Transcoder(new LegacyTranscoder())).ConfigureAwait(false);
            var content = getResult.ContentAs<string>();

            var expected = new
            {
                foo = "bar",
                bar = "bar",
                name = "mike"
            };
            Assert.Equal(JsonConvert.SerializeObject(expected), content);
        }

        [Fact]
        public async Task Test_When_Connection_Fails_It_Is_Recreated()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);

            try
            {
                await collection.LookupInAsync("docId", builder =>
                {
                    builder.Get("doc.path", isXattr: true);
                    builder.Count("path", isXattr: true); //will fail and cause server to close connection
                }).ConfigureAwait(false);
            }
            catch
            {
                // ignored -
                // The code above will force the server to abort the socket;
                // the connection will be reestablished and the code below should succeed
            }

            await collection.UpsertAsync(DocumentKey,  new {foo = "bar", bar = "foo"}).ConfigureAwait(false);;
        }

        [Fact]
        public async Task Test_MutateInAsync_Upsert_And_Xattr_Doc()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);

            var result = await collection.MutateInAsync("foo", specs =>
                {
                    specs.Upsert("key", "value", true, true);
                    specs.Upsert("name", "mikeSmith");
                },
                options => options.StoreSemantics(StoreSemantics.Upsert)).ConfigureAwait(false);

            var lookupResult = await collection.LookupInAsync("foo", specs => specs.Get("key", true));
            Assert.Equal("value", (string)lookupResult.ContentAs<string>(0));
        }

        [CouchbaseVersionDependentFact(MinVersion = "6.6.0")]
        public async Task MutateIn_CreateAsDeleted_Creates_Tombstone()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            var documentKey = nameof(MutateIn_CreateAsDeleted_Creates_Tombstone) + Guid.NewGuid().ToString();
            var result = await collection.MutateInAsync(documentKey, specs =>
                {
                    specs.Upsert("key", "value", true, true);
                 },
                options =>
                    options.StoreSemantics(StoreSemantics.Upsert)
                        .Expiry(TimeSpan.FromSeconds(30))
                        .CreateAsDeleted(true)).ConfigureAwait(false);

            _ = await Assert.ThrowsAnyAsync<DocumentNotFoundException>(() => collection.GetAsync(documentKey));

            var lookupResult = await collection.LookupInAsync(documentKey,
                specs => specs.Get("key", true),
                opts => opts.AccessDeleted(true));
            Assert.Equal("value", (string)lookupResult.ContentAs<string>(0));

            Assert.True(lookupResult.IsDeleted);

            var lookupWithMissingXattr = await collection.LookupInAsync(documentKey,
                specs => specs.Get("txn.id", isXattr: true).Get("txn.stgd", isXattr: true).Get("$document", isXattr: true),
                opts => opts.AccessDeleted(true));
            Assert.True(lookupWithMissingXattr.IsDeleted);
            var docMeta = lookupWithMissingXattr.ContentAs<JObject>(2);
            Assert.NotNull(docMeta);
        }

        [Fact]
        public async Task MutateIn_Remove_Succeeds()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            var documentKey = nameof(MutateIn_Remove_Succeeds);
            var o = JObject.FromObject(new
            {
                title = "Sample",

                description = (string) null
            });

            await collection.UpsertAsync(documentKey, o);
            var result = await collection.MutateInAsync(documentKey, specs =>
            {
                specs.Remove("title").Insert<string>("title", null);
                specs.Insert<string>("newKey", null, true);
                specs.Upsert<string>("title", null, true);
            });
        }

        #region Helpers

        private class TestDoc
        {
            public string Foo { get; set; }

            public string Bar { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string Name { get; set; }

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int Bah { get; set;}

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int Zzz { get; set; }

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            public int Xxx { get; set; }
        }

        #endregion
    }
}
