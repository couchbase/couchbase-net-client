using System;
using System.Threading.Tasks;
using Couchbase.CombinationTests.Fixtures;
using Xunit;

namespace Couchbase.CombinationTests.Tests.Compression.Snappier
{
    public class CompressionTests : IClassFixture<CouchbaseFixture>
    {
        public CompressionTests(CouchbaseFixture fixture)
        {
            _fixture = fixture;
        }

        private readonly CouchbaseFixture _fixture;

        [Fact]
        public async Task InsertAndGet()
        {
            var collection = await _fixture.GetDefaultCollection().ConfigureAwait(false);
            var key = Guid.NewGuid().ToString();

            try
            {
                await collection.InsertAsync(key, new {name = "mike", data=new string('X', 500)}).ConfigureAwait(false);

                using (var result = await collection.GetAsync(key).ConfigureAwait(false))
                {
                    var content = result.ContentAs<dynamic>();

                    Assert.Equal("mike", (string) content.name);
                    Assert.Equal(500, ((string) content.data).Length);
                }
            }
            finally
            {
                await collection.RemoveAsync(key).ConfigureAwait(false);
            }
        }
    }
}
