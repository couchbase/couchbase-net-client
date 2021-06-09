using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.Utils;
using Couchbase.Management.Collections;
using Couchbase.Test.Common.Utils;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.IntegrationTests.Services.Analytics
{
    public class AnalyticsTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private static readonly string ScopeName = $"myScope{RandomString()}";
        private static readonly string CollectionName = $"myCollection{RandomString()}";
        private static readonly string FilteredCollection = $"myFilteredCollection{RandomString()}";
        private readonly ITestOutputHelper _output;

        private static string RandomString()
        {
            {
                var length = 7;

                // creating a StringBuilder object()
                var strBuild = new StringBuilder();
                var random = new Random();
                for (int i = 0; i < length; i++)
                {
                    var flt = random.NextDouble();
                    var shift = Convert.ToInt32(Math.Floor(25 * flt));
                    var letter = Convert.ToChar(shift + 65);
                    strBuild.Append(letter);
                }
                return strBuild.ToString();
            }
        }

        public AnalyticsTests(ClusterFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        private class TestRequest
        {
            [JsonProperty("greeting")]
            public string Greeting { get; set; }
        }

        [Fact]
        public async Task Execute_Query()
        {
            const string statement = "SELECT \"hello\" as greeting;";

            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var analyticsResult = await cluster.AnalyticsQueryAsync<TestRequest>(statement).ConfigureAwait(false);
            var result = await analyticsResult.ToListAsync();

            Assert.Single(result);
            Assert.Equal("hello", result.First().Greeting);
        }

        [Fact]
        public async Task Test_Ingest()
        {
            const string statement = "SELECT \"hello\" as greeting;";

            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var result = await cluster.IngestAsync<dynamic>(
                statement,
                await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false),
                options =>
                {
                    options.Timeout(TimeSpan.FromSeconds(75));
                    options.Expiry(TimeSpan.FromDays(1));
                }
            ).ConfigureAwait(false);

            Assert.True(result.Any());
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_Collections_DataverseCollectionQuery()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var bucket = await _fixture.Cluster.BucketAsync("default").ConfigureAwait(false);

            var dataverseName = bucket.Name + "." + ScopeName;
            var collectionManager = (CollectionManager)bucket.Collections;
            var analytics = cluster.AnalyticsIndexes;

            try
            {
                await using var dataverseDisposer = DisposeCleaner.DropDataverseOnDispose(analytics, dataverseName, _output);
                await collectionManager.CreateScopeAsync(ScopeName).ConfigureAwait(false);
                await using var scopeDispose = DisposeCleaner.DropScopeOnDispose(collectionManager, ScopeName, _output);
                var collectionSpec = new CollectionSpec(ScopeName, CollectionName);

                await Task.Delay(TimeSpan.FromSeconds(1));
                await collectionManager.CreateCollectionAsync(collectionSpec).ConfigureAwait(false);

                await Task.Delay(TimeSpan.FromSeconds(1));
                var collectionExistsResult =
                    await collectionManager.CollectionExistsAsync(collectionSpec).ConfigureAwait(false);
                Assert.True(collectionExistsResult);

                await bucket.Scope(ScopeName).Collection(CollectionName).UpsertAsync("KEY1", new {bar = "foo"});

                await analytics.CreateDataverseAsync(dataverseName).ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(5));

                var statement = $"CREATE ANALYTICS COLLECTION {FilteredCollection} ON {bucket.Name}.{ScopeName}.{CollectionName}";
                await cluster.AnalyticsQueryAsync<TestRequest>(statement).ConfigureAwait(false);
                await using var analyticsCollectionDisposer = new DisposeCleanerAsync(() =>
                    cluster.AnalyticsQueryAsync<dynamic>($"DROP ANALYTICS COLLECTION {FilteredCollection}"),
                    _output
                );

                await Task.Delay(TimeSpan.FromSeconds(5));

                var selectStatement = $"SELECT * FROM `{FilteredCollection}`";
                var analyticsResult2 = await cluster.AnalyticsQueryAsync<TestRequest>(selectStatement).ConfigureAwait(false);

                var result = await analyticsResult2.ToListAsync().ConfigureAwait(false);
                Assert.True(result.Any());
            }
            catch (Exception e)
            {
                _output.WriteLine("oops{0}", e);
            }
            finally
            {
                // give some time befor the cleanups happen.
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
    }
}
