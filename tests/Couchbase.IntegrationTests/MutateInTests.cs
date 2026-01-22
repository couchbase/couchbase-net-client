using System;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.Utils;
using Couchbase.KeyValue;
using Couchbase.Test.Common.Fixtures;
using Couchbase.Test.Common.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Couchbase.IntegrationTests
{
    public class MutateInTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;
        private readonly ITestOutputHelper _outputHelper;

        public MutateInTests(ClusterFixture fixture, ITestOutputHelper outputHelper)
        {
            _fixture = fixture;
            _outputHelper = outputHelper;
        }

        [CouchbaseVersionDependentTheory(MinVersion = "7.0.0")]
        [InlineData(true)]
        [InlineData(false)]
        public async Task MutateInTests_With_PreserveTtl(bool preserveTtl)
        {
            var docId = "MutateInTests.MutateIn_With_PreserveTtl";
            var collection = await _fixture.GetDefaultCollectionAsync();

            await using var docDisposer = DisposeCleaner.RemoveDocument(collection, docId, _outputHelper);
            await collection.InsertAsync(docId, new {Bar = "Foo"}, options => options.Expiry(TimeSpan.FromMinutes(1)));
            var result =
                await collection.MutateInAsync(docId, builder => builder.Insert("Foo", "Bar", true),
                    options => { options.PreserveTtl(preserveTtl); });
            Assert.True(result.Cas > 0);
        }
    }
}
