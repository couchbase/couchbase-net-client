using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Test.Common.Fixtures;
using Moq;
using Newtonsoft.Json;
using Xunit;
#pragma warning disable CS0618 // Type or member is obsolete

namespace Couchbase.IntegrationTests.Services.Views
{
    public class CouchbaseBucketViewQueryTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public CouchbaseBucketViewQueryTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Test_ViewQuery_HasKeys()
        {
            var bucket = await _fixture.Cluster.BucketAsync("beer-sample");
            var result = await bucket.ViewQueryAsync<string[], object>("beer", "brewery_beers", options =>
            {
                options.Limit(10);
            });

            var count = 0;
            await foreach (var row in result)
            {
                Assert.NotNull(row.Key);

                count++;
            }

            Assert.Equal(10, count);
        }

        [Fact]
        public async Task Test_ViewQuery_HasValues()
        {
            var bucket = await _fixture.Cluster.BucketAsync("beer-sample");
            var result = await bucket.ViewQueryAsync<object, int>("beer", "by_location", options =>
            {
                options.Limit(10);
            });

            await foreach (var row in result)
            {
                Assert.NotEqual(0, row.Value);
            }
        }

        [Fact]
        public async Task Test_QueryAsyncNoDeadlock()
        {
            // NCBC-1074 https://issues.couchbase.com/browse/NCBC-1074
            // Using an asynchronous view query within an MVC Web API action causes
            // a deadlock if you wait for the result synchronously.

            var bucket = await _fixture.Cluster.BucketAsync("beer-sample");

            var context = new Mock<SynchronizationContext>
            {
                CallBase = true
            };

            SynchronizationContext.SetSynchronizationContext(context.Object);
            try
            {
                bucket.ViewQueryAsync<dynamic, dynamic>("beer", "brewery_beers", options => options.Limit(1))
#pragma warning disable xUnit1031
                    .Wait();
#pragma warning restore xUnit1031

                // If view queries are incorrectly awaiting on the current SynchronizationContext
                // We will see calls to Post or Send on the mock

                context.Verify(m => m.Post(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
                context.Verify(m => m.Send(It.IsAny<SendOrPostCallback>(), It.IsAny<object>()), Times.Never);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(null);
            }
        }

        [Fact]
        public async Task Use_Streaming()
        {
            var bucket = await _fixture.Cluster.BucketAsync("beer-sample");
            var result = await bucket.ViewQueryAsync<object, object>("beer", "brewery_beers", options =>
            {
                options.Limit(10);
            });

            var count = 0;
            await foreach (var row in result)
            {
                count++;
                Assert.NotNull(row);
                Assert.NotNull(row.Id);
            }

            Assert.Equal(10, count);
            Assert.InRange(result.MetaData.TotalRows, 7303u, 7500u);
        }

        [Fact]
        public async Task Can_Submit_Lots_of_Keys()
        {
            var bucket = await _fixture.Cluster.BucketAsync("beer-sample");
            await bucket.ViewQueryAsync<object, object>("beer", "brewery_beers",
                options =>
                {
                    options.Keys(Enumerable.Range(1, 1000).Select(i => $"key-{i}"));

                });
        }
    }

    public class Beer
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("abv")]
        public decimal Abv { get; set; }

        [JsonProperty("ibu")]
        public decimal Ibu { get; set; }

        [JsonProperty("srm")]
        public decimal Srm { get; set; }

        [JsonProperty("upc")]
        public int Upc { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("brewery_id")]
        public string BreweryId { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("style")]
        public string Style { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
