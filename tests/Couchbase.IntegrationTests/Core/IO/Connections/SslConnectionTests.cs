using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Xunit;

namespace Couchbase.IntegrationTests.Core.IO.Connections
{
    public class SslConnectionTests : IClassFixture<SslClusterFixture>
    {
        private readonly SslClusterFixture _fixture;

        public SslConnectionTests(SslClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task ParallelOperations()
        {
            var bucket = await _fixture.GetDefaultBucket().ConfigureAwait(false);
            var collection = await bucket.DefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            try
            {
                await collection.InsertAsync(key, new {name = "mike"}).ConfigureAwait(false);

                async Task DoOneHundredGets()
                {
                    for (var i = 0; i < 100; i++)
                    {
                        using var result = await collection.GetAsync(key).ConfigureAwait(false);

                        var content = result.ContentAs<dynamic>();

                        Assert.Equal("mike", (string) content.name);
                    }
                }

                var parallelTasks = Enumerable.Range(1, 8)
                    .Select(_ => DoOneHundredGets())
                    .ToList();

                await Task.WhenAll(parallelTasks).ConfigureAwait(false);
            }
            finally
            {
                await collection.RemoveAsync(key).ConfigureAwait(false);
            }
        }
    }
}
