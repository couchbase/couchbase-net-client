using System;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.TestData;
using Couchbase.KeyValue;
using Xunit;

namespace Couchbase.IntegrationTests
{
    public class GetReplicaTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public GetReplicaTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Can_get_any_replica()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            var key = Guid.NewGuid().ToString();
            var person = Person.Create();

            try
            {
                await collection.InsertAsync(key, person).ConfigureAwait(false);

                var result = await collection.GetAnyReplicaAsync(key).ConfigureAwait(false);
                Assert.NotEqual(ulong.MinValue, result.Cas);
                Assert.Null(result.ExpiryTime);

                var retrievedPerson = result.ContentAs<Person>();
                Assert.Equal(person.name, retrievedPerson.name);
            }
            finally
            {
                await collection.RemoveAsync(key).ConfigureAwait(false);
            }
        }

        [Fact]
        public async Task Can_get_all_replicas()
        {
            var collection = await _fixture.GetDefaultCollectionAsync().ConfigureAwait(false);
            var key = Guid.NewGuid().ToString();
            var person = Person.Create();

            try
            {
                await collection.InsertAsync(key, person).ConfigureAwait(false);

                var result = await Task.WhenAll(collection.GetAllReplicasAsync(key)).ConfigureAwait(false);
                Assert.Contains(result, x => x.IsActive);
                Assert.Contains(result, x => !x.IsActive);

                foreach (var p in result)
                {
                    Assert.NotEqual(ulong.MinValue, p.Cas);
                    Assert.Null(p.ExpiryTime);

                    var retrievedPerson = p.ContentAs<Person>();
                    Assert.Equal(person.name, retrievedPerson.name);
                }
            }
            finally
            {
                await collection.RemoveAsync(key).ConfigureAwait(false);
            }
        }
    }
}
