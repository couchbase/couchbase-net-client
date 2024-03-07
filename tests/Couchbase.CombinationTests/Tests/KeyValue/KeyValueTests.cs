using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.CombinationTests.Fixtures;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.IntegrationTests.Utils;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
using CollectionSpec = Couchbase.Management.Collections.CollectionSpec;

namespace Couchbase.CombinationTests.Tests.KeyValue
{
    [Collection(CombinationTestingCollection.Name)]
    public class KeyValueTests
    {
        private readonly CouchbaseFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;
        private TestHelper _testHelper;

        public KeyValueTests(CouchbaseFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            _outputHelper = outputHelper;
            _testHelper = new TestHelper(fixture);
        }

        [Fact]
        public async Task Test_TryGetAsync_KeyNotFound()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = "NonExistentKey";

            var getResult = await col.TryGetAsync(doc1);
            Assert.False(getResult.Exists);
        }

        [Fact]
        public async Task Test_TryGetAsync_KeyFound()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = "ExistentKey";
            await col.UpsertAsync(doc1, new { DocThatExists = true });

            var getResult = await col.TryGetAsync(doc1);
            Assert.True(getResult.Exists);

            var content = getResult.ContentAs<dynamic>();
            Assert.NotNull(content);
        }

        [Fact]
        public async Task Test_GetAsync()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new {Name = doc1}, options => options.Expiry(TimeSpan.FromSeconds(10)));
            var result = await col.GetAsync(doc1);

            var content = result.ContentAs<JObject>();
            Assert.Equal(doc1, content?.SelectToken("name").Value<string>());
        }

        [Fact]
        public async Task Test_UpsertAsync()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new {Name = doc1}, options => options.Expiry(TimeSpan.FromSeconds(10)));
            var result = await col.GetAsync(doc1);

            var content = result.ContentAs<JObject>();
            Assert.Equal(doc1, content?.SelectToken("name").Value<string>());
        }

        [Fact]
        public async Task Test_ReplaceAsync()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new {Name = doc1}, options => options.Expiry(TimeSpan.FromSeconds(10)));
            var result = await col.GetAsync(doc1);
            var content = result.ContentAs<JObject>();
            Assert.Equal(doc1, content?.SelectToken("name").Value<string>());

            await col.ReplaceAsync(doc1, new {Name = "changed"}, options => options.Expiry(TimeSpan.FromSeconds(10)));
            var result1 = await col.GetAsync(doc1);
            var content1 = result1.ContentAs<JObject>();
            Assert.Equal("changed", content1?.SelectToken("name").Value<string>());
        }

        [Fact]
        public async Task Test_RemoveAsync()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new {Name = doc1}, options => options.Expiry(TimeSpan.FromSeconds(10)));
            var result = await col.GetAsync(doc1);
            var content = result.ContentAs<JObject>();
            Assert.Equal(doc1, content?.SelectToken("name").Value<string>());

            await col.RemoveAsync(doc1);
            await Assert.ThrowsAsync<DocumentNotFoundException>(async () => await col.GetAsync(doc1));
        }

        [Fact]
        public async Task Test_ExistsAsync()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new {Name = doc1}, options => options.Expiry(TimeSpan.FromSeconds(10)));
            var result = await col.ExistsAsync(doc1);
            Assert.True(result.Exists);

            await col.RemoveAsync(doc1);
            var result1 = await col.ExistsAsync(doc1);
            Assert.False(result1.Exists);
        }

        [Fact]
        public async Task Test_TouchAsync()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new {Name = doc1}, options => options.Expiry(TimeSpan.FromSeconds(2)));
            var result = await col.ExistsAsync(doc1);
            Assert.True(result.Exists);

            await col.TouchAsync(doc1, TimeSpan.FromSeconds(2));

            await Task.Delay(TimeSpan.FromSeconds(3));
            var result1 = await col.ExistsAsync(doc1);
            Assert.False(result1.Exists);
        }

        [Fact]
        public async Task Test_TouchWithCasAsync()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new {Name = doc1}, options => options.Expiry(TimeSpan.FromSeconds(2)));
            var upsertResult = await col.ExistsAsync(doc1);
            Assert.True(upsertResult.Exists);

            var touchResult = await col.TouchWithCasAsync(doc1, TimeSpan.FromSeconds(2));
            Assert.NotNull(touchResult);
            Assert.NotEqual(0ul, touchResult?.Cas);

            await Task.Delay(TimeSpan.FromSeconds(3));
            var existsResult = await col.ExistsAsync(doc1);
            Assert.False(existsResult.Exists);
        }

        [Fact]
        public async Task Test_GetAndTouchAsync()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new {Name = doc1}, options => options.Expiry(TimeSpan.FromSeconds(2)));
            var result = await col.GetAndTouchAsync(doc1, TimeSpan.FromSeconds(2));

            var content = result.ContentAs<JObject>();
            Assert.Equal(doc1, content?.SelectToken("name").Value<string>());

            await Task.Delay(TimeSpan.FromSeconds(3));
            var result1 = await col.ExistsAsync(doc1);
            Assert.False(result1.Exists);
        }

        [Fact(Skip = "NCBC-3204")]
        public async Task Test_GetAndLockAsync_Locked()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new {Name = doc1}, options => options.Expiry(TimeSpan.FromHours(2)));

            var result = await col.GetAndLockAsync(doc1, TimeSpan.FromHours(2));
            var content = result.ContentAs<JObject>();
            Assert.Equal(doc1, content?.SelectToken("name").Value<string>());

            await Assert.ThrowsAsync<DocumentLockedException>(async () =>
                await col.UpsertAsync(doc1, new {Name = "Test_GetAndLockAsync"}));
        }

        [Fact]
        public async Task Test_GetAndLockAsync_LockExpired()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new {Name = doc1}, options => options.Expiry(TimeSpan.FromSeconds(2)));

            var result = await col.GetAndLockAsync(doc1, TimeSpan.FromSeconds(2));
            var content = result.ContentAs<JObject>();
            Assert.Equal(doc1, content?.SelectToken("name").Value<string>());

            await Task.Delay(TimeSpan.FromSeconds(3));
            var result1 = await col.UpsertAsync(doc1, new {Name = "Test_GetAndLockAsync"});
        }

        [Fact]
        public async Task Test_GetAndLockAsync_Unlocked()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new {Name = doc1}, options => options.Expiry(TimeSpan.FromSeconds(2)));

            var result = await col.GetAndLockAsync(doc1, TimeSpan.FromSeconds(2));
            var content = result.ContentAs<JObject>();
            Assert.Equal(doc1, content?.SelectToken("name").Value<string>());

            await col.UnlockAsync(doc1, result.Cas);

            await col.UpsertAsync(doc1, new {Name = "Test_GetAndLockAsync"});

            var result1 = await col.GetAsync(doc1);
            var content1 = result1.ContentAs<JObject>();
            Assert.Equal("Test_GetAndLockAsync", content1?.SelectToken("name").Value<string>());
        }

        [Fact]
        public async Task Test_LookupInAsync_Exists()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new { Name = doc1, Id = 1, Items = new[] { 1, 2, 3 } },
                options => options.Expiry(TimeSpan.FromSeconds(2)));

            var result = await col.LookupInAsync(doc1, specs => specs.Exists("name"));
            Assert.True(result.Exists(0));
        }

        [Fact]
        public async Task Test_LookupInAsync_Get()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new { Name = doc1, Id = 1, Items = new[] { 1, 2, 3 } },
                options => options.Expiry(TimeSpan.FromSeconds(2)));

            var result = await col.LookupInAsync(doc1, specs => specs.Get("id"));
            Assert.True(result.Exists(0));
        }

        [Fact]
        public async Task Test_LookupInAsync_Count()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new { Name = doc1, Id = 1, Items = new[] { 1, 2, 3 } },
                options => options.Expiry(TimeSpan.FromSeconds(2)));

            var result = await col.LookupInAsync(doc1, specs => specs.Count("items"));
            Assert.True(result.Exists(0));
        }

        [Fact]
        public async Task Test_LookupInAsync_All()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new {Name = doc1, Id = 1, Items = new[] {1, 2, 3}},
                options => options.Expiry(TimeSpan.FromSeconds(2)));

            var result = await col.LookupInAsync(doc1, specs => specs.Exists("name").Get("id").Count("items"));
            Assert.True(result.Exists(0));
            Assert.Equal(1, result.ContentAs<int>(1));
            Assert.Equal(3, result.ContentAs<int>(2));
        }

        [CouchbaseVersionDependentFact(MinVersion = "8.0.0")]
        public async Task Test_LookupInAnyReplicaAsync_All()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new {Name = doc1, Id = 1, Items = new[] {1, 2, 3}},
                options => options.Expiry(TimeSpan.FromSeconds(2)));

            var specs = new LookupInSpec[]
            {
                LookupInSpec.Exists("name"),
                LookupInSpec.Get("id"),
                LookupInSpec.Count("items"),
            };

            var result = await col.LookupInAnyReplicaAsync(doc1, specs);
            Assert.True(result.Exists(0));
            Assert.Equal(1, result.ContentAs<int>(1));
            Assert.Equal(3, result.ContentAs<int>(2));
            Assert.NotNull(result.IsReplica);
        }

        [CouchbaseVersionDependentFact(MinVersion = "8.0.0")]
        public async Task Test_LookupInAllReplicasAsync_All()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new {Name = doc1, Id = 1, Items = new[] {1, 2, 3}},
                options => options.Expiry(TimeSpan.FromSeconds(2)));

            var specs = new LookupInSpec[]
            {
                LookupInSpec.Exists("name"),
                LookupInSpec.Get("id"),
                LookupInSpec.Count("items"),
            };

            var results = col.LookupInAllReplicasAsync(doc1, specs);
            int resultCount = 0;
            int isReplicaCount = 0;
            await foreach (var result in results)
            {
                Assert.True(result.Exists(0));
                Assert.Equal(1, result.ContentAs<int>(1));
                Assert.Equal(3, result.ContentAs<int>(2));
                Assert.NotNull(result.IsReplica);
                resultCount++;
                if (result.IsReplica == true)
                {
                    isReplicaCount++;
                }
            }

            if (resultCount > 1)
            {
                Assert.NotEqual(0, isReplicaCount);
            }
        }

        [Fact]
        public async Task Test_LookupInAsync_GetFull()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new {Name = doc1, Id = 2, Items = new[] {8, 1, 2, 3}},
                options => options.Expiry(TimeSpan.FromSeconds(2)));

            var result = await col.LookupInAsync(doc1, specs => specs.GetFull());
            var foo = result.ContentAs<Foo>(0);

            Assert.Equal(doc1, foo.Name);
            Assert.Equal(2, foo.Id);
            Assert.Equal(4, foo.Items.Count);
        }

        [Fact]
        public async Task Test_MutateInAsync_Chained()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new {Name = doc1, Id = 1, Items = new[] {1, 2, 3}, Age=10},
                options => options.Expiry(TimeSpan.FromSeconds(2)));

            var result = await col.MutateInAsync(doc1, specs => specs.Replace("name", "null").
                Insert("insert", 2).
                Remove("id").
                Upsert("age", 2).
                ArrayPrepend("items", 5).
                ArrayAppend("items", 7));

            Assert.True(result.Cas > 0);

            var result1 = await col.GetAsync(doc1);
            var content = result1.ContentAs<JObject>();
            Assert.Equal("null", content?.SelectToken("name").Value<string>());
            Assert.Equal(2, content?.SelectToken("insert").Value<int>());
            Assert.Null(content?.SelectToken("id"));
            Assert.Equal(5, content?.SelectToken("items").ToObject<List<int>>().First());
            Assert.Equal(7, content?.SelectToken("items").ToObject<List<int>>().Last());
        }

        [Fact]
        public async Task Test_MutateInAsync_Replace()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new { Name = doc1, Id = 1, Items = new[] { 1, 2, 3 } },
                options => options.Expiry(TimeSpan.FromSeconds(2)));

            var result = await col.MutateInAsync(doc1, specs => specs.Replace("name", "null"));
            Assert.True(result.Cas > 0);

            var result1 = await col.GetAsync(doc1);
            var content = result1.ContentAs<JObject>();
            Assert.Equal("null", content?.SelectToken("name").Value<string>());
        }

        [Fact]
        public async Task Test_MutateInAsync_Remove()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new { Name = doc1, Id = 1, Items = new[] { 1, 2, 3 } },
                options => options.Expiry(TimeSpan.FromSeconds(2)));

            var result = await col.MutateInAsync(doc1, specs => specs.Remove("name"));
            Assert.True(result.Cas > 0);

            var result1 = await col.GetAsync(doc1);
            var content = result1.ContentAs<JObject>();
            Assert.Null(content?.SelectToken("name"));
        }

        [Fact]
        public async Task Test_MutateInAsync_Insert()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new { Name = doc1, Id = 1, Items = new[] { 1, 2, 3 } },
                options => options.Expiry(TimeSpan.FromSeconds(2)));

            var result = await col.MutateInAsync(doc1, specs => specs.Insert("insert", 2));
            Assert.True(result.Cas > 0);

            var result1 = await col.GetAsync(doc1);
            var content = result1.ContentAs<JObject>();
            Assert.Equal(2, content?.SelectToken("insert").Value<int>());
        }

        [Fact]
        public async Task Test_MutateInAsync_ArrayAddUnique()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new {Name = doc1, Id = 1, Items = new[] {1, 2, 3}},
                options => options.Expiry(TimeSpan.FromSeconds(2)));

            var result = await col.MutateInAsync(doc1, specs => specs.ArrayAddUnique("items", 9));
            Assert.True(result.Cas > 0);

            var result1 = await col.GetAsync(doc1);
            var content = result1.ContentAs<JObject>();
            Assert.Contains(9, content?.SelectToken("items").ToObject<List<int>>());
        }

        [Fact]
        public async Task Test_MutateInAsync_ArrayAppend()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new { Name = doc1, Id = 1, Items = new[] { 1, 2, 3 } },
                options => options.Expiry(TimeSpan.FromSeconds(2)));

            var result = await col.MutateInAsync(doc1, specs => specs.ArrayAppend("items", 9));
            Assert.True(result.Cas > 0);

            var result1 = await col.GetAsync(doc1);
            var content = result1.ContentAs<JObject>();
            Assert.Equal(9, content?.SelectToken("items").ToObject<List<int>>().Last());
        }

        [Fact]
        public async Task Test_MutateInAsync_ArrayPrepend()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new { Name = doc1, Id = 1, Items = new[] { 1, 2, 3 } },
                options => options.Expiry(TimeSpan.FromSeconds(2)));

            var result = await col.MutateInAsync(doc1, specs => specs.ArrayPrepend("items", 9));
            Assert.True(result.Cas > 0);

            var result1 = await col.GetAsync(doc1);
            var content = result1.ContentAs<JObject>();
            Assert.Equal(9, content?.SelectToken("items").ToObject<List<int>>().First());
        }

        [Fact(Skip = "https://issues.couchbase.com/browse/NCBC-3068")]
        public async Task Test_MutateInAsync_ArrayInsert()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new { Name = doc1, Id = 1, Items = new[] { 1, 2, 3 } },
                options => options.Expiry(TimeSpan.FromSeconds(2)));

            var result = await col.MutateInAsync(doc1, specs => specs.ArrayInsert("items", new[] {8, 9}));
            Assert.True(result.Cas > 0);

            var result1 = await col.GetAsync(doc1);
            var content = result1.ContentAs<JObject>();
            Assert.Contains(8, content?.SelectToken("items").ToObject<List<int>>());
            Assert.Contains(9, content?.SelectToken("items").ToObject<List<int>>());
        }

        [Fact]
        public async Task Test_MutateInAsync_SetDoc()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new { Name = doc1, Id = 1, Items = new[] { 1, 2, 3 } },
                options => options.Expiry(TimeSpan.FromSeconds(2)));

            var result = await col.MutateInAsync(doc1, specs => specs.SetDoc(new {Strange = "Days"}));
            Assert.True(result.Cas > 0);

            var result1 = await col.GetAsync(doc1);
            var content = result1.ContentAs<JObject>();
            Assert.Equal("Days", content.SelectToken("strange").Value<string>());
        }

        [Fact]
        public async Task Test_MutateInAsync_Increment()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new { Counter = 1 },
                options => options.Expiry(TimeSpan.FromSeconds(2)));

            var result = await col.MutateInAsync(doc1, specs => specs.Increment("counter", (ulong)2));
            Assert.True(result.Cas > 0);

            var result1 = await col.GetAsync(doc1);
            var content = result1.ContentAs<JObject>();
            Assert.Equal(3, content?.SelectToken("counter").Value<int>());
        }

        [Fact]
        public async Task Test_MutateInAsync_Decrement()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new { Counter = 3 },
                options => options.Expiry(TimeSpan.FromSeconds(2)));

            var result = await col.MutateInAsync(doc1, specs => specs.Decrement("counter", (ulong)2));
            Assert.True(result.Cas > 0);

            var result1 = await col.GetAsync(doc1);
            var content = result1.ContentAs<JObject>();
            Assert.Equal(1, content?.SelectToken("counter").Value<int>());
        }

        [Fact]
        public async Task Test_LookupInAsync_DoesNot_Exists()
        {
            var col = await _fixture.GetDefaultCollection();
            var doc1 = "Test_LookupInAsync_DoesNot_Exists";

            await col.UpsertAsync(doc1, new { Name = doc1, Id = 1, Items = new[] { 1, 2, 3 } },
                                    options => options.Expiry(TimeSpan.FromSeconds(1000)));

            var result = await col.LookupInAsync(doc1, specs => specs.Exists("name1"));
            Assert.False(result.Exists(0));
        }

        [Fact]
        public async Task Test_GetAnyReplica_Throws_DocumentUnretrievable()
        {
            var id = "Test-" + Guid.NewGuid();
            var collection = await _fixture.GetDefaultCollection().ConfigureAwait(false);

            await collection.UpsertAsync(id, new { Name = id, Id = 1, Items = new[] { 1, 2, 3 } });

            var specs = new List<LookupInSpec>();
            specs.Add(LookupInSpec.Get("name"));

            await Assert.ThrowsAsync<DocumentUnretrievableException>(() => collection.GetAnyReplicaAsync("wrongId"));

            await collection.RemoveAsync(id).ConfigureAwait(false);
        }

        [Fact]
        public async Task Test_KV_Operation_Properly_Records_Its_Latency()
        {
            var id = "Test" + Guid.NewGuid();
            var collectionName = "TestCollection" + new Random().Next();

            var bucket = await _fixture.GetDefaultBucket().ConfigureAwait(false);
            var scope = await _fixture.GetDefaultScope().ConfigureAwait(false);

            // Create and get collection on default bucket/scope
            await bucket.Collections.CreateCollectionAsync("_default", collectionName, new CreateCollectionSettings()).ConfigureAwait(false);
            await _testHelper.WaitUntilCollectionIsPresent(collectionName).ConfigureAwait(false);
            var collection = await scope.CollectionAsync(collectionName).ConfigureAwait(false);

            //Upsert a doc so that the client caches the Cid
            await collection.UpsertAsync(id, new { Content = "hello" }).ConfigureAwait(false);
            await _testHelper.WaitUntilDocumentIsPresent(id, collectionName).ConfigureAwait(false);

            // Drop collection
            await bucket.Collections.DropCollectionAsync("_default", collectionName).ConfigureAwait(false);
            await _testHelper.WaitUntilCollectionIsDropped(collectionName).ConfigureAwait(false);

            var stopwatch = Stopwatch.StartNew();
            var exception = await Record.ExceptionAsync(async () => await collection.GetAsync(id).ConfigureAwait(false)).ConfigureAwait(false);
            stopwatch.Stop();

            _outputHelper.WriteLine("Exception: " + exception!.Message);
            _outputHelper.WriteLine($"Test measured Time for operation = {stopwatch.Elapsed.TotalSeconds}");

            Assert.Equal(exception!.GetType(), typeof(UnambiguousTimeoutException));
            Assert.True(exception.Message.Contains("The Get operation") || exception.Message.Contains("The GetCidByName operation"));
            Assert.True(exception.Message.Contains("00:00:02.5"));
        }

        private class Foo
        {
            public string Name { get; set; }

            public int Id { get; set; }

            public List<int> Items { get; set; }

            public int Age { get; set; }
        }
    }
}
