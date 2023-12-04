using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.KeyValue;
using Couchbase.Stellar.CombinationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.Stellar.CombinationTests.KeyValue
{
    [Collection(StellarTestCollection.Name)]
    public class StellarKvTests
    {
        private readonly StellarFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public StellarKvTests(StellarFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task Exists()
        {
            var collection = await _fixture.DefaultCollection();
            var doc1 = Guid.NewGuid().ToString();

            await collection.UpsertAsync(doc1, new { Name = doc1 }, options => options.Expiry(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
            var result = await collection.ExistsAsync(doc1).ConfigureAwait(false);
            Assert.True(result.Exists);

            await collection.RemoveAsync(doc1);
            var result1 = await collection.ExistsAsync(doc1).ConfigureAwait(false);
            Assert.False(result1.Exists);
        }

        [Fact]
        public async Task Get()
        {
            var collection = await _fixture.DefaultCollection();
            var id = Guid.NewGuid().ToString();
            await collection.UpsertAsync(id, new { Content = "Test" }).ConfigureAwait(false);
            var result = await collection.GetAsync(id).ConfigureAwait(false);
            Assert.Equal(result.ContentAs<dynamic>().ToString(), "{\"content\":\"Test\"}");
            await collection.RemoveAsync(id).ConfigureAwait(false);
        }

        [Fact]
        public async Task Get_Throws_Document_Not_Found()
        {
            var collection = await _fixture.DefaultCollection();

            var exception = await Record.ExceptionAsync( () => collection.GetAsync("fake_doc")).ConfigureAwait(false);
            Assert.IsType<DocumentNotFoundException>(exception);
        }

        private record UpsertSampleDoc(string Id, string Updated);

        [Fact]
        public async Task Upsert()
        {
            var collection = await _fixture.DefaultCollection();
            var dt = DateTimeOffset.Now.ToString("R");
            var id = "UnitTestUpsert01";
            var contentObj = new UpsertSampleDoc(id, dt);
            var upsertResponse = await collection.UpsertAsync(id, contentObj).ConfigureAwait(false);
            Assert.NotNull(upsertResponse);
            Assert.NotEqual(0u, upsertResponse.Cas);

            var getResponse = await collection.GetAsync(id).ConfigureAwait(false);
            Assert.NotNull(getResponse);
            Assert.NotEqual(0u, getResponse.Cas);
            var deserializedDoc = getResponse.ContentAs<UpsertSampleDoc>();
            Assert.Equal(dt, deserializedDoc?.Updated);

            await collection.RemoveAsync(id).ConfigureAwait(false);
        }

        [Fact]
        public async Task Insert()
        {
            var collection = await _fixture.DefaultCollection();
            var dt = DateTimeOffset.Now.ToString("R");
            var id = "UnitTestInsert01" + Guid.NewGuid();
            var contentObj = new UpsertSampleDoc(id, dt);
            var insertOptions = new InsertOptions().Expiry(TimeSpan.FromSeconds(120));
            var mutationResult = await collection.InsertAsync(id, contentObj, insertOptions).ConfigureAwait(false);
            Assert.NotNull(mutationResult);
            Assert.NotEqual(0u, mutationResult.Cas);

            var getResponse = await collection.GetAsync(id).ConfigureAwait(false);
            Assert.NotNull(getResponse);
            Assert.NotEqual(0u, getResponse.Cas);
            var deserializedDoc = getResponse.ContentAs<UpsertSampleDoc>();
            Assert.Equal(dt, deserializedDoc?.Updated);

            await collection.RemoveAsync(id);
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private record Hobby(string Name, double average_annual_expenditures);
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private record Hobbyist(string Name, IEnumerable<Hobby> Hobbies);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private record HobbyCustomers(string Id, string Group, IEnumerable<Hobbyist> Customers,
            string? Placeholder = null)
        {
            [JsonExtensionData] public Dictionary<string, JsonElement> Data { get; set; } = null;
        };

        [Fact]
        public async Task LookupIn()
        {
            var collection = await _fixture.DefaultCollection();
            var id = "LookupIn_Test001" + Guid.NewGuid();
            var customers = GetExampleHobbyCustomers(id);

            await collection.InsertAsync(id, customers, opts => opts.Expiry(TimeSpan.FromMinutes(5))).ConfigureAwait(false);

            try
            {
                var response = await collection.LookupInAsync(id, specs =>
                    specs.Get("customers")
                        .GetFull()
                        .Exists("doesnotexist")
                        .Exists("group")
                        .Get("doesnotexist")
                        .Count("customers")
                );

                Assert.NotNull(response);
                Assert.True(response.Exists(0), "'customers' exists, originally a Get");
                Assert.True(response.Exists(1), "full document exists");
                Assert.False(response.Exists(2), "path that does not exist");
                Assert.True(response.Exists(3), "path that does exist");
                Assert.True(response.Exists(5), "'customers' as Exists");
                var hobbyist = response.ContentAs<HobbyCustomers>(1);
            }
            catch (Exception e)
            {
                _outputHelper.WriteLine(e.ToString());
                throw;
            }

            await collection.RemoveAsync(id).ConfigureAwait(false);
        }


        [Fact]
        public async Task MutateIn()
        {
            var id = "MutateIn_Test001" + Guid.NewGuid();
            _outputHelper.WriteLine("DocID = {0}", id);
            var collection = await _fixture.DefaultCollection();
            var customers = GetExampleHobbyCustomers(id) with { Placeholder = "removeme" };

            var insertResult = await collection.InsertAsync(id, customers, opts => opts.Expiry(TimeSpan.FromMinutes(5)));

            try
            {
                var primo = new Hobbyist(Name: "Primo", Hobbies: Enumerable.Empty<Hobby>());
                var sigurd = new Hobbyist(Name: "Sigurd", Hobbies: Enumerable.Empty<Hobby>());
                var bob = new Hobbyist(Name: "Bob", Hobbies: Enumerable.Empty<Hobby>());
                var sam = new Hobbyist(Name: "Sam",
                    Hobbies: new Hobby[]
                    {
                        new Hobby("cheese", 2_032.00)
                    }
                );

                var mutateResponse = await collection.MutateInAsync(id, specs =>
                    specs
                        .Increment("review_count", (ulong)10, createPath: true)
                        .Decrement("review_count", (ulong)2)
                        .Replace("group", "most_valued_customers")
                        .Insert("dict_add", new Hobby("dict_add_hobby", 1.003))
                        .Upsert("foo", "bar")
                        .ArrayPrepend("customers", primo)
                        .ArrayAppend("customers", sam)
                        .ArrayAddUnique("new_array", 95, createPath: true) // Should this fail?
                        .ArrayAddUnique("new_array", 101)
                        .ArrayInsert("customers[1]", sigurd)
                        .Remove("placeholder")
                    , opts => opts.Cas(insertResult.Cas));

                Assert.NotNull(mutateResponse);
                Assert.NotEqual((ulong)0, mutateResponse.Cas);

                var getResponse = await collection.TryGetAsync(id);
                Assert.NotNull(getResponse);
                Assert.Equal(mutateResponse.Cas, getResponse.Cas);
                var finalData = getResponse.ContentAs<HobbyCustomers>();
                Assert.Collection(finalData.Customers,
                    h => Assert.Equal(primo.Name, h.Name),
                    h => Assert.Equal(sigurd.Name, h.Name),
                    h => Assert.Equal(customers.Customers.First().Name, h.Name),
                    h => Assert.Equal(customers.Customers.Last().Name, h.Name),
                    h => Assert.Equal(sam.Name, h.Name)
                    );
                Assert.Null(finalData.Placeholder);
                if (finalData.Data is not null)
                {
                    Assert.Equal(8, finalData.Data["review_count"].GetInt32());
                    Assert.True(finalData.Data.ContainsKey("dict_add"));
                    Assert.Equal("bar", finalData.Data["foo"].GetString());
                    Assert.Collection(finalData.Data["new_array"].EnumerateArray(),
                        el => Assert.Equal(95, el.GetInt32()),
                        el => Assert.Equal(101, el.GetInt32())
                    );
                }
            }
            catch (Exception e)
            {
                _outputHelper.WriteLine(e.ToString());
                throw;
            }

            await collection.RemoveAsync(id).ConfigureAwait(false);

        }

        [Fact]
        public async Task GetAllReplicas()
        {
            var collection = await _fixture.DefaultCollection().ConfigureAwait(false);
            var key = Guid.NewGuid().ToString();
            var person = new {Name = "Lambda", Age = 100};

            try
            {
                await collection.InsertAsync(key, person).ConfigureAwait(false);

                var results = await Task.WhenAll(collection.GetAllReplicasAsync(key)).ConfigureAwait(false);
                foreach (var result in results)
                {
                    Assert.True(result.IsActive is true or false);
                }

                foreach (var p in results)
                {
                    Assert.NotEqual(ulong.MinValue, p.Cas);
                    Assert.Null(p.ExpiryTime);

                    var retrievedPerson = p.ContentAs<dynamic>();
                    Assert.Contains(person.Name, retrievedPerson.ToString());
                    Assert.Contains(person.Age.ToString(), retrievedPerson.ToString());
                }
            }
            finally
            {
                await collection.RemoveAsync(key).ConfigureAwait(false);
            }
        }

        private static HobbyCustomers GetExampleHobbyCustomers(string id)
        {
            var customers = new HobbyCustomers(id, "hobbyists",
                new List<Hobbyist>()
                {
                    new Hobbyist("Joe", new List<Hobby>()
                    {
                        new Hobby("golf", 6_324.00),
                        new Hobby("poker", 25.00),
                        new Hobby("magic the gathering", 10_382.00),
                    }),
                    new Hobbyist("Janet", new List<Hobby>()
                    {
                        new Hobby("classic car restoration", 512_000.23),
                    })
                }
            );
            return customers;
        }
    }
}
