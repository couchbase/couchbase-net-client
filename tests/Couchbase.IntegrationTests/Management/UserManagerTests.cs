using System;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Management.Buckets;
using Couchbase.Management.Users;
using Xunit;

namespace Couchbase.IntegrationTests.Management
{
    public class UserManagerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public UserManagerTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task CreateAndDropUSer()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            await cluster.Users.UpsertUserAsync(new User("usermgr_test") {
                Password = "password"
            }).ConfigureAwait(false);

            await cluster.Users.DropUserAsync("usermgr_test").ConfigureAwait(false);
        }
    }
}
