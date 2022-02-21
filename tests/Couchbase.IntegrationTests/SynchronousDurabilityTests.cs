using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.Utils;
using Couchbase.KeyValue;
using Xunit;

namespace Couchbase.IntegrationTests
{
    public class SynchronousDurabilityTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public SynchronousDurabilityTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [CouchbaseVersionDependentTheory(MinVersion = "6.5.0")]
        [InlineData(DurabilityLevel.None)]
        [InlineData(DurabilityLevel.Majority)]
        [InlineData(DurabilityLevel.MajorityAndPersistToActive)]
        [InlineData(DurabilityLevel.PersistToMajority)]
        public async Task Upsert_with_durability(DurabilityLevel durabilityLevel)
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);

            // Upsert will throw exception if durability is not met
            await collection.UpsertAsync(
                "id",
                new {name = "mike"},
                options => options.Durability(durabilityLevel)
            ).ConfigureAwait(false);
        }
    }
}
