using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Couchbase.Core.IO.Authentication.X509;
using Couchbase.KeyValue;
using Couchbase.Test.Common.Utils;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.Stellar.CombinationTests.WrappedClient
{
    public class StellarKv
    {
        private readonly ITestOutputHelper _outputHelper;

        public StellarKv(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Theory]
        [InlineData("protostellar")]
        [InlineData("couchbase")]
        [InlineData("couchbases")]
        public async Task Exists(string protocol)
        {
            var col = await DefaultCollection(protocol);
            var doc1 = Guid.NewGuid().ToString();

            await col.UpsertAsync(doc1, new { Name = doc1 }, options => options.Expiry(TimeSpan.FromSeconds(10)));
            var result = await col.ExistsAsync(doc1);
            Assert.True(result.Exists);

            await col.RemoveAsync(doc1);
            var result1 = await col.ExistsAsync(doc1);
            Assert.False(result1.Exists);
        }

        private record UpsertSampleDoc(string id, string updated);

        [Theory]
        [InlineData("protostellar")]
        [InlineData("couchbase")]
        public async Task Upsert(string protocol)
        {
            var collection = await DefaultCollection(protocol);
            var dt = DateTimeOffset.Now.ToString("R");
            var id = "UnitTestUpsert01";
            var contentObj = new UpsertSampleDoc(id, dt);
            var upsertResponse = await collection.UpsertAsync(id, contentObj);
            Assert.NotNull(upsertResponse);
            Assert.NotEqual(0u, upsertResponse.Cas);

            var getResponse = await collection.GetAsync(id);
            Assert.NotNull(getResponse);
            Assert.NotEqual(0u, getResponse.Cas);
            var deserializedDoc = getResponse.ContentAs<UpsertSampleDoc>();
            Assert.Equal(dt, deserializedDoc?.updated);
        }

        [Theory]
        [InlineData("protostellar")]
        [InlineData("couchbase")]
        [InlineData("couchbases")]
        public async Task Insert(string protocol)
        {
            var collection = await DefaultCollection(protocol);
            var dt = DateTimeOffset.Now.ToString("R");
            var id = "UnitTestInsert01" + Guid.NewGuid().ToString();
            var contentObj = new UpsertSampleDoc(id, dt);
            var insertOptions = new InsertOptions().Expiry(TimeSpan.FromSeconds(120));
            var mutationResult = await collection.InsertAsync(id, contentObj, insertOptions);
            Assert.NotNull(mutationResult);
            Assert.NotEqual(0u, mutationResult.Cas);

            var getResponse = await collection.GetAsync(id);
            Assert.NotNull(getResponse);
            Assert.NotEqual(0u, getResponse.Cas);
            var deserializedDoc = getResponse.ContentAs<UpsertSampleDoc>();
            Assert.Equal(dt, deserializedDoc?.updated);

            await collection.RemoveAsync(id);

            // TODO: use Exists to check document was removed.
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private record Hobby(string name, double average_annual_expenditures);
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private record Hobbyist(string name, IEnumerable<Hobby> hobbies);

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private record HobbyCustomers(string id, string group, IEnumerable<Hobbyist> customers,
            string? placeholder = null)
        {
            [JsonExtensionData] public Dictionary<string, JsonElement> Data { get; set; } = null;
        };

        [Theory]
        [InlineData("protostellar")]
        [InlineData("couchbase")]
        [InlineData("couchbases")]
        public async Task LookupIn(string protocol)
        {
            var collection = await DefaultCollection(protocol);
            var id = "LookupIn_Test001" + Guid.NewGuid();
            var customers = GetExampleHobbyCustomers(id);

            await collection.InsertAsync(id, customers, opts => opts.Expiry(TimeSpan.FromMinutes(5)));

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

        }


        [Theory]
        [InlineData("protostellar")]
        [InlineData("couchbase")]
        [InlineData("couchbases")]
        public async Task MutateIn(string protocol)
        {
            var id = "MutateIn_Test001" + Guid.NewGuid();
            _outputHelper.WriteLine("DocID = {0}", id);
            var collection = await DefaultCollection(protocol);
            var customers = GetExampleHobbyCustomers(id) with { placeholder = "removeme" };

            var insertResult = await collection.InsertAsync(id, customers, opts => opts.Expiry(TimeSpan.FromMinutes(5)));

            try
            {
                var primo = new Hobbyist(name: "Primo", hobbies: Enumerable.Empty<Hobby>());
                var sigurd = new Hobbyist(name: "Sigurd", hobbies: Enumerable.Empty<Hobby>());
                var bob = new Hobbyist(name: "Bob", hobbies: Enumerable.Empty<Hobby>());
                var sam = new Hobbyist(name: "Sam",
                    hobbies: new Hobby[]
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
                Assert.Collection(finalData.customers,
                    h => Assert.Equal(primo.name, h.name),
                    h => Assert.Equal(sigurd.name, h.name),
                    h => Assert.Equal(customers.customers.First().name, h.name),
                    h => Assert.Equal(customers.customers.Last().name, h.name),
                    h => Assert.Equal(sam.name, h.name)
                    );
                Assert.Null(finalData.placeholder);
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

        private async Task<ICouchbaseCollection> DefaultCollection(string protocol)
        {
            var opts = new ClusterOptions()
            {
                UserName = "Administrator",
                Password = "password"
            };

            var loggerFactory = new TestOutputLoggerFactory(_outputHelper);
            opts.WithLogging(loggerFactory);

            var connectionString = $"{protocol}://localhost";
            // var connectionString = "protostellar://sdksng.couchbase6.com:443";
            if (connectionString.Contains("//localhost"))
            {
                opts.KvIgnoreRemoteCertificateNameMismatch = true;
                opts.HttpIgnoreRemoteCertificateMismatch = true;
            }

            var cluster = await StellarCluster.ConnectAsync(connectionString, opts);
            var bucket = await cluster.BucketAsync("default");
            var scope = bucket.Scope("_default");
            var collection = scope.Collection("_default");
            return collection;
        }
    }
}
