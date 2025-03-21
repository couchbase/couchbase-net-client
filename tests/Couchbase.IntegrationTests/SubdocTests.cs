using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Serializers;
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

            using var result = await collection.GetAsync("Can_Return_Expiry()", options=>options.Expiry()).ConfigureAwait(false);
            Assert.NotNull(result.ExpiryTime);
        }

        [Fact]
        public async Task LookupIn_Can_Return_FullDoc()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            await collection.UpsertAsync("LookupIn_Can_Return_FullDoc()", new {foo = "bar", bar = "foo"}, options =>options.Expiry(TimeSpan.FromHours(1))).ConfigureAwait(false);

            using var result = await collection.LookupInAsync("LookupIn_Can_Return_FullDoc()", builder=>builder.GetFull());
            var doc = result.ContentAs<dynamic>(0);
            Assert.NotNull(doc);
        }

        [Fact]
        public async Task LookupIn_Xattr_In_Any_Order()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            await collection.UpsertAsync(nameof(LookupIn_Xattr_In_Any_Order), new { foo = "bar", bar = "foo" }, options => options.Expiry(TimeSpan.FromHours(1))).ConfigureAwait(false);
            using var result = await collection.LookupInAsync(nameof(LookupIn_Xattr_In_Any_Order), specs =>
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

            using var result = await collection.LookupInAsync(DocumentKey, ops =>
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
                using var result = await collection.LookupInAsync<TestDoc>(key, ops =>
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

            using var result = await collection.LookupInAsync(DocumentKey, ops =>
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

            using var result = await collection.LookupInAsync(DocumentKey, new[]
            {
                LookupInSpec.Get("foo"),
                LookupInSpec.Get("bar")
            }).ConfigureAwait(false);

            Assert.Equal("bar", result.ContentAs<string>(0));
            Assert.Equal("foo", result.ContentAs<string>(1));
        }

        [Fact]
        [Obsolete()]
        public async Task Can_perform_mutate_in_signedcounters()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            await collection.UpsertAsync(DocumentKey,  new {foo = "bar", bar = "foo"}).ConfigureAwait(false);

            using var _ = await collection.MutateInAsync(DocumentKey, ops =>
            {
                ops.Upsert("name", "mike");
                ops.Replace("bar", "bar");
                ops.Insert("bah", 0);
                ops.Increment("zzz", 10, true);
                ops.Decrement("xxx", 5, true);
                ops.Increment("zzzneg", -10, true);
                ops.Decrement("xxxneg", -5, true);
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
                    xxx = -5,
                    zzzneg = -10,
                    xxxneg = -5 // Here an obsolete defect on signed long Spec is asserted for inverse of a negative delta not applied
                };
                Assert.Equal(JsonConvert.SerializeObject(expected), content);
            }
        }

        [Fact]
        public async Task Can_perform_mutate_in_unsignedcounters()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            await collection.UpsertAsync(DocumentKey,  new {foo = "bar", bar = "foo"}).ConfigureAwait(false);

            using var _ = await collection.MutateInAsync(DocumentKey, ops =>
            {
                ops.Upsert("name", "mike");
                ops.Replace("bar", "bar");
                ops.Insert("bah", 0);
                ops.Increment("zzz", 10UL, true);
                ops.Decrement("xxx", 5UL, true);
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
        [Obsolete()]
        public async Task Can_perform_mutate_in_via_lambda_signedcounters()
        {
            var key = Guid.NewGuid().ToString();

            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            await collection.UpsertAsync(key, new TestDoc {Foo = "bar", Bar = "foo"}).ConfigureAwait(false);

            try
            {
                using var res = await collection.MutateInAsync<TestDoc>(key, ops =>
                {
                    ops.Upsert(p => p.Name, "mike");
                    ops.Replace(p => p.Bar, "bar");
                    ops.Insert(p => p.Bah, 0);
                    ops.Increment(p => p.Zzz, 10, true);
                    ops.Decrement(p => p.Xxx, 5, true);
                    ops.Increment(p => p.Zzzneg, -10, true);
                    ops.Decrement(p => p.Xxxneg, -5, true);
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
                        xxx = -5,
                        zzzneg = -10,
                        xxxneg = -5 // Here an obsolete defect on signed long Spec is asserted for inverse of a negative delta not applied
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
        public async Task Can_perform_mutate_in_via_lambda_unsignedcounters()
        {
            var key = Guid.NewGuid().ToString();

            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            await collection.UpsertAsync(key, new TestDoc {Foo = "bar", Bar = "foo"}).ConfigureAwait(false);

            try
            {
                using var res = await collection.MutateInAsync<TestDoc>(key, ops =>
                {
                    ops.Upsert(p => p.Name, "mike");
                    ops.Replace(p => p.Bar, "bar");
                    ops.Insert(p => p.Bah, 0);
                    ops.Increment(p => p.Zzz, 10UL, true);
                    ops.Decrement(p => p.Xxx, 5UL, true);
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

            using var mutateResult = await collection.MutateInAsync(docId, ops =>
            {
                ops.Upsert("name", "mike", true);
                ops.Upsert("txnid", "pretend_this_is_a_guid", createPath: true, isXattr: true);
                ops.Decrement("xxx", (ulong)5, true);
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

            using var result = await collection.MutateInAsync(DocumentKey, new[]
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
                using var _ = await collection.LookupInAsync("docId", builder =>
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

            using var result = await collection.MutateInAsync("foo", specs =>
                {
                    specs.Upsert("key", "value", true, true);
                    specs.Upsert("name", "mikeSmith");
                },
                options => options.StoreSemantics(StoreSemantics.Upsert)).ConfigureAwait(false);

            var lookupResult = await collection.LookupInAsync("foo", specs => specs.Get("key", true));
            Assert.Equal("value", (string)lookupResult.ContentAs<string>(0));
        }

        [Fact]
        public async Task LookupIn_BadPathNoException()
        {
            (var documentKey, var collection) = await PrepDoc();

            // LookupIn should not throw if one path is bad.
            using var lookupInResult = await collection.LookupInAsync(documentKey, specs => specs.Get("foo").Get("doesNotExist"));
            var fooValue = lookupInResult.ContentAs<string>(0);
            Assert.Equal("bar", fooValue);
            Assert.False(lookupInResult.Exists(1));
        }

        private async Task<(string documentKey, ICouchbaseCollection collection)> PrepDoc([CallerMemberName]string testName = nameof(SubdocTests))
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            var documentKey = testName + Guid.NewGuid().ToString();
            var doc = new { foo = "bar", baz = "baz" };
            var insertResult = await collection.InsertAsync(documentKey, doc, opts => opts.Expiry(TimeSpan.FromMinutes(10)));
            return (documentKey, collection);
        }

        [Fact]
        public async Task MutateIn_PathInvalid()
        {
            (var documentKey, var collection) = await PrepDoc();
            using var t = collection.MutateInAsync(documentKey,
                specs => specs.Upsert("foo", "bar_updated").Replace("baz\\$$-foo", "anything"),
                opts => opts.StoreSemantics(StoreSemantics.Replace));

            var ex = await Assert.ThrowsAsync<PathInvalidException>(() => t);

        }

        [Fact]
        public async Task MutateIn_PathTooBig()
        {
            (var documentKey, var collection) = await PrepDoc();
            var tooLong = string.Join(".", System.Linq.Enumerable.Repeat("a", 300));
            using var t = collection.MutateInAsync(documentKey,
                specs => specs.Upsert("foo", "bar_updated").Replace("baz." + tooLong, "anything"),
                opts => opts.StoreSemantics(StoreSemantics.Replace));

            var ex = await Assert.ThrowsAsync<PathTooBigException>(() => t);
        }

        [Fact]
        public async Task MutateIn_PathMismatch()
        {
            (var documentKey, var collection) = await PrepDoc();
            var tooLong = string.Join(".", System.Linq.Enumerable.Repeat("a", 16));
            using var t = collection.MutateInAsync(documentKey,
                specs => specs.Upsert("foo", "bar_updated").Replace("baz." + tooLong, "anything"),
                opts => opts.StoreSemantics(StoreSemantics.Replace));

            var ex = await Assert.ThrowsAsync<PathMismatchException>(() => t);
        }

        [Fact]
        public async Task MutateIn_PathNotFound()
        {
            (var documentKey, var collection) = await PrepDoc();
            using var t = collection.MutateInAsync(documentKey,
                specs => specs.Replace("doesNotExist", "anything"),
                opts => opts.StoreSemantics(StoreSemantics.Replace));

            var ex = await Assert.ThrowsAsync<PathNotFoundException>(() => t);
        }

        [CouchbaseVersionDependentFact(MinVersion = "6.6.0")]
        public async Task MutateIn_CreateAsDeleted_Creates_Tombstone()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            var documentKey = nameof(MutateIn_CreateAsDeleted_Creates_Tombstone) + Guid.NewGuid().ToString();
            using var result = await collection.MutateInAsync(documentKey, specs =>
                {
                    specs.Upsert("key", "value", true, true);
                 },
                options =>
                    options.StoreSemantics(StoreSemantics.Upsert)
                        .Expiry(TimeSpan.FromSeconds(30))
                        .CreateAsDeleted(true)).ConfigureAwait(false);

            _ = await Assert.ThrowsAnyAsync<DocumentNotFoundException>(() => collection.GetAsync(documentKey));

            using var lookupResult = await collection.LookupInAsync(documentKey,
                specs => specs.Get("key", true),
                opts => opts.AccessDeleted(true));
            Assert.Equal("value", (string)lookupResult.ContentAs<string>(0));

            Assert.True(lookupResult.IsDeleted);

            using var lookupWithMissingXattr = await collection.LookupInAsync(documentKey,
                specs => specs.Get("txn.id", isXattr: true).Get("txn.stgd", isXattr: true).Get("$document", isXattr: true),
                opts => opts.AccessDeleted(true));
            Assert.True(lookupWithMissingXattr.IsDeleted);
            var docMeta = lookupWithMissingXattr.ContentAs<object>(2);
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
            using var result = await collection.MutateInAsync(documentKey, specs =>
            {
                specs.Remove("title").Insert<string>("title", null);
                specs.Insert<string>("newKey", null, true);
                specs.Upsert<string>("title", null, true);
            });
        }

        [Fact]
        public async Task MutateIn_SetDoc_UsesTranscoder()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            var documentKey = nameof(MutateIn_SetDoc_UsesTranscoder);
            byte[] o = [1, 2, 3, 4];
            var transcoder = new LegacyTranscoder(DefaultSerializer.Instance);

            using var _ = await collection.MutateInAsync(documentKey,
                builder =>
                {
                    builder.SetDoc(o);
                    builder.Upsert("test_attr", "foo", isXattr: true);
                },
                options => options.Transcoder(transcoder).StoreSemantics(StoreSemantics.Upsert));

            using var result = await collection.LookupInAsync(documentKey,
                builder =>
                {
                    builder.GetFull();
                    builder.Get("test_attr", isXattr: true);
                },
                options => options.Transcoder(transcoder));

            Assert.Equal(o, result.ContentAs<byte[]>(0));
            //Assert.Equal("foo", result.ContentAs<string>(1));
        }

        [Fact]
        public async Task MutateIn_ReplaceBodyWithXattr_Succeeds()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            var documentKey = nameof(MutateIn_ReplaceBodyWithXattr_Succeeds);
            await collection.UpsertAsync(documentKey, new { foo = "bar", bar = "foo", xxx = 0 }).ConfigureAwait(false);
            var newDocBody = new { bar = "foo2", foo = "bar2", xxx = 3 };

            // put the newDocBody in test_attr xattr...
            using (await collection.MutateInAsync(documentKey, builder =>
            {
                builder.Upsert("test_attr", newDocBody, isXattr: true, createPath: true);
            }));

            using (await collection.MutateInAsync(documentKey, builder =>
            {
                builder.ReplaceBodyWithXattr("test_attr");
            }));

            // verify the document body changed.  NOTE: by comparing as strings, we run the risk
            // of having the order change though the key/value pairs are all equal. If you change
            // the newDocBody (or the old one), you may need to adjust the order of one or the
            // other to match.
            var getResult = await collection.GetAsync(documentKey, options => options.Transcoder(new LegacyTranscoder())).ConfigureAwait(false);
            Assert.Equal(getResult.ContentAs<string>(), JsonConvert.SerializeObject(newDocBody));
        }

        [Fact]
        public async Task MutateIn_ReviveDocument_Succeeds()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            var documentKey = nameof(MutateIn_ReviveDocument_Succeeds) + Guid.NewGuid();
            // first, lets create a tombstone
            using (await collection.MutateInAsync(documentKey, builder =>
                {
                    builder.Upsert("test_attr", "foo", isXattr: true);
                }, options => options.StoreSemantics(StoreSemantics.Upsert).CreateAsDeleted(true))
                .ConfigureAwait(false));

            // verify it is a tombstone
            var lookupInResult = await collection.LookupInAsync(documentKey, builder =>
            {
                builder.GetFull();
            }, options => options.AccessDeleted(true)).ConfigureAwait(false);
            Assert.True(lookupInResult.IsDeleted);

            // now revive (we can use the ReplaceBodyWithXattr since we do that with txns)...
            using (await collection.MutateInAsync(documentKey, builder =>
                   {
                       builder.ReplaceBodyWithXattr("test_attr");
                   }, options => options.ReviveDocument(true)).ConfigureAwait(false));
            // verify not a tombstone
            var lookupInResult2 = await collection.LookupInAsync(documentKey, builder =>
            {
                builder.GetFull();
            }, options => options.AccessDeleted(true)).ConfigureAwait(false);
            Assert.False(lookupInResult2.IsDeleted);
        }

        [Fact]
        public async Task  MutateIn_ReviveDocument_FailsIfDocumentExists()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            var documentKey = nameof(MutateIn_ReviveDocument_FailsIfDocumentExists);

            // regular old non-tombstone document
            await collection.UpsertAsync(documentKey, new { foo = "bar" }).ConfigureAwait(false);

            using var task = collection.MutateInAsync(documentKey, builder =>
            {
                builder.Upsert("test_attr", "foo", isXattr: true);
            }, options => options.ReviveDocument(true));

            await Assert.ThrowsAsync<DocumentAlreadyAliveException>(async () => await task.ConfigureAwait(false));
        }

        #region Helpers

        private class TestDoc
        {
            public string Foo { get; set; }

            public string Bar { get; set; }

            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
            public string Name { get; set; }

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
            public int Bah { get; set;}

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
            public int Zzz { get; set; }

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
            public int Xxx { get; set; }

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
            public int Zzzneg { get; set; }

            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
            [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
            public int Xxxneg { get; set; }
        }

        #endregion
    }
}
