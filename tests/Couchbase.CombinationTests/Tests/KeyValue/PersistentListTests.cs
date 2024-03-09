using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Couchbase.KeyValue;

namespace Couchbase.CombinationTests.Tests.KeyValue
{
    [Collection(CombinationTestingCollection.Name)]
    public class PersistentListTests
    {
        private readonly CouchbaseFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public PersistentListTests(CouchbaseFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task Test_AddAsync()
        {
            var key1 = Guid.NewGuid().ToString();
            var col = await _fixture.GetDefaultCollection();
            var list = col.List<int?>(key1);

            try
            {
                await list.AddAsync(4);
                Assert.True(await list.ContainsAsync(4));
                await foreach (var d in list)
                {
                    Assert.NotNull(d);
                }
                Assert.True(await list.RemoveAsync(4));
            }
            finally
            {
                await col.RemoveAsync(key1);
            }
        }
    }
}
