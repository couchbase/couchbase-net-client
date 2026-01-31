using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Management.Query;
using Couchbase.Management.Search;
using Couchbase.Management.Views;
using Couchbase.Query;
using Couchbase.Test.Common.Fixtures;
using Couchbase.Views;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
#pragma warning disable CS0618 // Type or member is obsolete

namespace Couchbase.IntegrationTests.Management
{
    [Collection("NonParallel")]
    public class VerifyEnvironment : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;
        public static readonly string[] TestBuckets = new[] { "default", "beer-sample", "travel-sample" };

        public VerifyEnvironment(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task VerifyBuckets()
        {
            var exceptions = new List<Exception>();
            foreach (var bucketName in TestBuckets)
            {
                try
                {
                    var bucket = await _fixture.Cluster.BucketAsync(bucketName);
                    await bucket.WaitUntilReadyAsync(TimeSpan.FromSeconds(10));
                    var pingResult = await bucket.PingAsync();
                    _outputHelper.WriteLine(JObject.FromObject(pingResult).ToString());
                }
                catch (Exception e)
                {
                    _outputHelper.WriteLine($"{bucketName} could not be opened and pinged.");
                    exceptions.Add(e);
                    throw;
                }
            }

            if (exceptions.Any())
            {
                throw new AggregateException(exceptions);
            }
        }

        [Fact]
        public async Task VerifyOrCreateQueryIndexes()
        {
            var exceptions = new List<Exception>();
            var missingPrimaries = new List<string>();
            var queryResult = await _fixture.Cluster.QueryAsync<JObject>("SELECT * FROM system:indexes");
            var rows = await queryResult.Rows.ToListAsync();
            foreach (var jobj in rows)
            {
                _outputHelper.WriteLine("from cluster query: " + jobj.ToString(Formatting.None));
            }

            foreach (var bucketName in TestBuckets)
            {
                _outputHelper.WriteLine($"Bucket: {bucketName}");
                var queryIndexes = (await _fixture.Cluster.QueryIndexes.GetAllIndexesAsync(bucketName)).ToList();
                foreach (var index in queryIndexes)
                {
                    _outputHelper.WriteLine("from bucket query: " + JObject.FromObject(index).ToString(Formatting.None));
                }

                if (queryIndexes.Any(qi => qi.IsPrimary
                && qi.State == "online"))
                {
                    _outputHelper.WriteLine($"Primary index found for '{bucketName}'");
                }
                else
                {
                    _outputHelper.WriteLine($"Missing primary index for '{bucketName}'");
                    missingPrimaries.Add(bucketName);
                }
            }

            foreach (var bucketName in missingPrimaries)
            {
                try
                {
                    await _fixture.Cluster.QueryIndexes.CreatePrimaryIndexAsync($"`{bucketName}`",
                        new CreatePrimaryQueryIndexOptions()
                        {
                            DeferredValue = false,
                            IgnoreIfExistsValue = true,
                        });
                }
                catch (Exception e)
                {
                    _outputHelper.WriteLine($"Failed to create primary index for '{bucketName}': {e.ToString()}");
                    exceptions.Add(e);
                }
            }

            Assert.Empty(exceptions);
        }

        [Theory]
        [InlineData("idx-travel", "travel-sample")]
        public async Task VerifyOrCreateFtsIndexes(string indexName, string bucketName)
        {
            try
            {
                var existingIndex = await _fixture.Cluster.SearchIndexes.GetIndexAsync(indexName);
                if (existingIndex.Type == "fulltext-index")
                {
                    _outputHelper.WriteLine($"'{indexName}' already exists.");
                    return;
                }
            }
            catch (Exception e)
            {
                _outputHelper.WriteLine($"Failure looking up '{indexName}': {e.ToString()}");
            }

            _outputHelper.WriteLine($"Attempting to create fulltext index '{indexName}'");
            var searchIndex = new SearchIndex()
            {
                Name = "idx-travel",
                SourceName = bucketName,
                SourceType = "couchbase",
                Type = "fulltext-index"
            };

            try
            {
                await _fixture.Cluster.SearchIndexes.UpsertIndexAsync(searchIndex, UpsertSearchIndexOptions.Default);
            }
            catch (HttpRequestException e)
            {
                _outputHelper.WriteLine(e.ToString());
                throw;
            }
        }

        [Fact]
        public async Task VerifyBeerSampleView()
        {
            var bucketName = "beer-sample";
            var viewName = "brewery_beers";
            var designDocName = "beer";
            var mapFunction = @"function(doc, meta) {
  switch(doc.type) {
  case ""brewery"":
            emit([meta.id]);
            break;
            case ""beer"":
            if (doc.brewery_id)
            {
                emit([doc.brewery_id, meta.id]);
            }
            break;
        }
    }";
            var reduce = "_count";
            var designDoc = new DesignDocument()
            {
                Name = designDocName,
                Views = new Dictionary<string, View>()
                {
                    {
                        viewName,
                        new View() {
                            Map = mapFunction,
                            Reduce = reduce
                        }
                    }
                }
            };

            var beerSample = await _fixture.Cluster.BucketAsync(bucketName);
            try
            {
                _ = await beerSample.ViewIndexes.GetDesignDocumentAsync(designDocName, DesignDocumentNamespace.Production);
                _outputHelper.WriteLine($"'{designDocName}' already exists.");
                return;
            }
            catch (DesignDocumentNotFoundException)
            {
                _outputHelper.WriteLine($"'{designDocName}' not found.");
            }

            _outputHelper.WriteLine($"Attempting to create '{designDocName}/{viewName}' view.");


            await beerSample.ViewIndexes.UpsertDesignDocumentAsync(designDoc, DesignDocumentNamespace.Production);

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < TimeSpan.FromSeconds(120))
            {
                try
                {
                    _ = await beerSample.ViewIndexes.GetDesignDocumentAsync(designDocName, DesignDocumentNamespace.Production);
                    break;
                }
                catch (DesignDocumentNotFoundException)
                {
                    _outputHelper.WriteLine("DesignDocumentNotFound.  Sleeping.");
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }
        }
    }
}
