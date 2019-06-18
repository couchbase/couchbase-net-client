using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Moq;
using Xunit;

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
        public async Task Test_QueryAsyncNoDeadlock()
        {
            // NCBC-1074 https://issues.couchbase.com/browse/NCBC-1074
            // Using an asynchronous view query within an MVC Web API action causes
            // a deadlock if you wait for the result synchronously.

            var bucket = await _fixture.Cluster.BucketAsync("beer-sample").ConfigureAwait(false);

            var context = new Mock<SynchronizationContext>
            {
                CallBase = true
            };

            SynchronizationContext.SetSynchronizationContext(context.Object);
            try
            {
                bucket.ViewQueryAsync<dynamic>("beer", "brewery_beers", options => options.WithLimit(1))
                    .Wait();

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
            var bucket = await _fixture.Cluster.BucketAsync("beer-sample").ConfigureAwait(false);
            var result = await bucket.ViewQueryAsync<dynamic>("beer", "brewery_beers", options =>
            {
                options.WithLimit(10);
            }).ConfigureAwait(false);

            var count = 0;
            foreach (var row in result.Rows)
            {
                count++;
                Assert.NotNull(row);
                Assert.NotNull(row.Id);
            }

            Assert.Equal(10, count);
            Assert.Equal(7303u, result.MetaData.TotalRows);
        }

        [Fact]
        public async Task Can_Submit_Lots_of_Keys()
        {
            var bucket = await _fixture.Cluster.BucketAsync("beer-sample").ConfigureAwait(false);
            await bucket.ViewQueryAsync<dynamic>("beer", "brewery_beers",
                options =>
                {
                    options.WithKeys(Enumerable.Range(1, 1000).Select(i => $"key-{i}"));

                }).ConfigureAwait(false);
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
