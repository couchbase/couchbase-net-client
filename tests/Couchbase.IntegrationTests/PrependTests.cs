using System;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.IO.Transcoders;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.KeyValue;
using Couchbase.Test.Common.Fixtures;
using Xunit;

namespace Couchbase.IntegrationTests
{
    public class PrependTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public PrependTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Can_prepend()
        {
            var collection = await _fixture.GetDefaultCollectionAsync();
            var key = Guid.NewGuid().ToString();

            try
            {
                await collection.InsertAsync(key, Encoding.UTF8.GetBytes("world"), options => options.Transcoder(new LegacyTranscoder()));
                await collection.Binary.PrependAsync(key, Encoding.UTF8.GetBytes("hello "));

                using (var result = await collection.GetAsync(key, options => options.Transcoder(new LegacyTranscoder())))
                {
                    Assert.Equal("hello world", Encoding.UTF8.GetString(result.ContentAs<byte[]>()));
                }
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }
    }
}
