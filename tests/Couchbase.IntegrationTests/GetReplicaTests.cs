using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.TestData;
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
            var collection = await _fixture.GetDefaultCollection();
            var key = Guid.NewGuid().ToString();
            var person = Person.Create();

            try
            {
                await collection.InsertAsync(key, person);

                var result = await collection.GetAnyReplicaAsync(key);
                Assert.NotEqual(ulong.MinValue, result.Cas);
                Assert.True(result.HasValue);
                Assert.Null(result.Expiry);

                var retrievedPerson = result.ContentAs<Person>();
                Assert.Equal(person.name, retrievedPerson.name);
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }

        [Fact]
        public async Task Can_get_all_replicas()
        {
            var collection = await _fixture.GetDefaultCollection();
            var key = Guid.NewGuid().ToString();
            var person = Person.Create();

            try
            {
                await collection.InsertAsync(key, person);

                var result = await Task.WhenAll(collection.GetAllReplicasAsync(key));
                Assert.Contains(result, x => x.IsMaster);
                Assert.Contains(result, x => !x.IsMaster);

                foreach (var p in result)
                {
                    Assert.NotEqual(ulong.MinValue, p.Cas);
                    Assert.True(p.HasValue);
                    Assert.Null(p.Expiry);

                    var retrievedPerson = p.ContentAs<Person>();
                    Assert.Equal(person.name, retrievedPerson.name);
                }
            }
            finally
            {
                await collection.RemoveAsync(key);
            }
        }
    }
}
