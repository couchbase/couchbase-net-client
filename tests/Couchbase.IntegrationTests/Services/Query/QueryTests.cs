using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Serializers;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.Utils;
using Couchbase.Query;
using Couchbase.Test.Common.Utils;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.IntegrationTests.Services.Query
{
    public class QueryTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _testOutputHelper;

        public QueryTests(ClusterFixture fixture, ITestOutputHelper testOutputHelper)
        {
            _fixture = fixture;
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task Test_Query()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            await cluster.QueryAsync<Poco>("SELECT default.* FROM `default` LIMIT 1;").ConfigureAwait(false);
        }

        [Fact]
        public async Task Test_Prepared()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            // execute prepare first time
            var result = await cluster.QueryAsync<dynamic>("SELECT default.* FROM `default` LIMIT 1;",
                options => options.AdHoc(false)).ConfigureAwait(false);
            Assert.Equal(QueryStatus.Success, result.MetaData.Status);

            // should use prepared plan
            var preparedResult = await cluster.QueryAsync<dynamic>("SELECT default.* FROM `default` LIMIT 1;",
                options => options.AdHoc(false)).ConfigureAwait(false);
            Assert.Equal(QueryStatus.Success, preparedResult.MetaData.Status);
        }

        [Fact]
        public async Task Test_QueryWithSynchronousStream()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            var result = await cluster.QueryAsync<dynamic>("SELECT default.* FROM `default` WHERE type=$name;",
                parameter =>
            {
                parameter.Parameter("name", "person");
            }).ConfigureAwait(false);

            // Non-streaming approach in C# 7
#if NET8_0_OR_GREATER
            foreach (var o in result.ToBlockingEnumerable())
#else
//This is a breaking change in .NET10, but cannot resolve System.Linq.Async on .net48
//This will need to be investigated further as I don't understand why I cannot conditional find ToEnumerable
            foreach (var o in result.ToEnumerable())
#endif
            {
                _testOutputHelper.WriteLine(JsonConvert.SerializeObject(o, Formatting.None));
            }
            result.Dispose();
        }

        [Fact]
        public async Task Test_QueryWithAsyncStreamCSharp7()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            var result = await cluster.QueryAsync<dynamic>("SELECT default.* FROM `default` WHERE type=$name;",
                parameter =>
                {
                    parameter.Parameter("name", "person");
                }).ConfigureAwait(false);

            // Async streaming approach in C# 7
            var enumerator = result.GetAsyncEnumerator();
            try
            {
                while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    _testOutputHelper.WriteLine(JsonConvert.SerializeObject(enumerator.Current, Formatting.None));
                }
            }
            finally
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
            }

            result.Dispose();
        }

        [Fact]
        public async Task Test_QueryWithAsyncStreamCSharp8()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            var result = await cluster.QueryAsync<dynamic>("SELECT default.* FROM `default` WHERE type=$name;",
                parameter =>
                {
                    parameter.Parameter("name", "person");
                }).ConfigureAwait(false);

            await foreach (var o in result.ConfigureAwait(false))
            {
                _testOutputHelper.WriteLine(JsonConvert.SerializeObject(o, Formatting.None));
            }

            result.Dispose();
        }

        [Fact]
        public async Task Test_RawQuery()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var bucket = await _fixture.GetDefaultBucket();
            var collection = await bucket.DefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            try
            {
                await collection.InsertAsync(key, new {name = "john"}).ConfigureAwait(false);

                using var result = await cluster
                    .QueryAsync<string>("SELECT RAW name FROM `default` WHERE name IS VALUED LIMIT 1;")
                    .ConfigureAwait(false);

                var found = false;
                await foreach (var name in result.ConfigureAwait(false))
                {
                    found = true;
                    _testOutputHelper.WriteLine(name);
                }

                Assert.True(found);
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.6.0")]
        public async Task Test_Query_Use_Replica()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            var bucket = await _fixture.GetDefaultBucket().ConfigureAwait(false);
            var scope = await bucket.DefaultScopeAsync().ConfigureAwait(false);
            var collection = await scope.CollectionAsync("_default").ConfigureAwait(false);

            string id = Guid.NewGuid().ToString();
            try
            {
                //Upsert document and query it with ScanConsistency
                await collection.InsertAsync(id, new[] { "content" }).ConfigureAwait(false);

                var options = new QueryOptions().ScanConsistency(QueryScanConsistency.RequestPlus).Metrics(true);
                Assert.Equal(false, options.UseReplicaHasValue);

                var resultConsistent = await cluster
                    .QueryAsync<dynamic>($"SELECT * FROM `{bucket.Name}` WHERE meta().id = \"{id}\"", options)
                    .ConfigureAwait(false);
                await foreach (var r in resultConsistent.Rows) continue;
                Assert.Equal(1, (int)resultConsistent.MetaData!.Metrics.ResultCount);

                //Query without UseReplica

                var resultNoReplica = await cluster
                    .QueryAsync<dynamic>($"SELECT * FROM `{bucket.Name}` WHERE meta().id = \"{id}\"", options)
                    .ConfigureAwait(false);
                await foreach (var r in resultNoReplica.Rows) continue;
                Assert.Equal(1, (int)resultNoReplica.MetaData!.Metrics.ResultCount);

                //Query with UseReplica
                options.UseReplica(true);
                Assert.Equal(true, options.UseReplicaHasValue);

                var resultWithReplica = await cluster
                    .QueryAsync<dynamic>($"SELECT * FROM `{bucket.Name}` WHERE meta().id = \"{id}\"", options)
                    .ConfigureAwait(false);
                await foreach (var r in resultWithReplica.Rows) continue;
                Assert.Equal(1, (int)resultWithReplica.MetaData!.Metrics.ResultCount);
            }
            finally
            {
                await collection.RemoveAsync(id).ConfigureAwait(false);
            }
        }

#if NET6_0_OR_GREATER

        [Fact]
        public async Task Test_InterpolatedQuery()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            var type = "hotel";
            var limit = 1;

            using var result = await cluster.QueryInterpolatedAsync<dynamic>($"SELECT `travel-sample`.* FROM `travel-sample` WHERE type={type} LIMIT {limit}")
                .ConfigureAwait(false);

            await foreach (var o in result.ConfigureAwait(false))
            {
                _testOutputHelper.WriteLine(JsonConvert.SerializeObject(o, Formatting.None));
            }
        }

        [Fact]
        public async Task Test_InterpolatedQueryWithPassedOptions()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            var type = "hotel";
            var limit = 1;

            var options = new QueryOptions().AdHoc(false); // This can be done inline, but we do it here so we can assert afterward
            using var result = await cluster.QueryInterpolatedAsync<dynamic>(options, $"SELECT `travel-sample`.* FROM `travel-sample` WHERE type={type} LIMIT {limit}")
                .ConfigureAwait(false);

            await foreach (var o in result.ConfigureAwait(false))
            {
                _testOutputHelper.WriteLine(JsonConvert.SerializeObject(o, Formatting.None));
            }

            _testOutputHelper.WriteLine(options.StatementValue);
            _testOutputHelper.WriteLine(options.GetAllParametersAsJson(DefaultSerializer.Instance));
        }

        [Fact]
        public async Task Test_InterpolatedQueryWithOptionsBuilder()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            var type = "hotel";
            var limit = 1;

            using var result = await cluster.QueryInterpolatedAsync<dynamic>(options => options.AdHoc(false),
                $"SELECT `travel-sample`.* FROM `travel-sample` WHERE type={type} LIMIT {limit}")
                .ConfigureAwait(false);

            await foreach (var o in result.ConfigureAwait(false))
            {
                _testOutputHelper.WriteLine(JsonConvert.SerializeObject(o, Formatting.None));
            }
        }

#endif

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_Query_BeginWork_Affinity()
        {
            // After a BEGIN WORK statement is issued, all queries with the same "txid" parameter should
            // go to the same query node.
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            string txid = string.Empty;
            Uri originalQueryNode;

            {
                using var span = new TestOutputSpan(_testOutputHelper);
                var results = await cluster.QueryAsync<Transaction>("BEGIN WORK", options => options.RequestSpan(span)).ConfigureAwait(false);
                originalQueryNode = results.MetaData?.LastDispatchedToNode;
                await foreach (var result in results.Rows)
                {
                    _testOutputHelper.WriteLine($"txid: {result.Txid}");
                    txid = result.Txid;
                }

                // originalQueryNode = span.Attributes.Where(kvp => kvp.Key == "net.peer.name").Select(kvp => kvp.Value).FirstOrDefault();
            }

            for (int i = 0; i < 100; i++)
            {
                using var querySpan = new TestOutputSpan(_testOutputHelper);
                var options = new QueryOptions().Parameter("txid", txid).RequestSpan(querySpan);
                options.LastDispatchedNode = originalQueryNode;
                var result = await cluster.QueryAsync<Poco>("SELECT default.* FROM `default` LIMIT 1;", options).ConfigureAwait(false);


                var thisQueryHost = querySpan.Attributes.Where(kvp => kvp.Key == "net.peer.name").Select(kvp => kvp.Value).FirstOrDefault();
                var thisQueryPort = querySpan.Attributes.Where(kvp => kvp.Key == "net.peer.port").Select(kvp => kvp.Value).FirstOrDefault();
                Assert.Equal(originalQueryNode.Host, thisQueryHost);
                Assert.Equal(originalQueryNode.Port.ToString(), thisQueryPort);
            }
        }

        // ReSharper disable UnusedType.Local
        private class Poco
        {
            public string Name { get; set; }
        }

        private class Transaction
        {
            public string Txid { get; set; }
        }

        // ReSharper restore UnusedType.Local
    }
}
