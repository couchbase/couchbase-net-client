using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Management.Users;
using Xunit;

namespace Couchbase.IntegrationTests.Management
{
    [Collection("NonParallel")]
    public class UserManagerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public UserManagerTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Test_CreateAndDropUser()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            await cluster.Users.UpsertUserAsync(new User("usermgr_test") {
                Password = "password"
            }).ConfigureAwait(false);

            await cluster.Users.DropUserAsync("usermgr_test").ConfigureAwait(false);
        }

        [Fact]
        public async Task Test_CreateAndDropUserWithBucket()
        {
            var name = "usermgr_bucket_role_test";
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            await cluster.Users.UpsertUserAsync(new User(name)
            {
                Password = "password",
                Roles = new List<Role>
                {
                    new Role("data_reader", "default")
                }
            }).ConfigureAwait(false);

            await cluster.Users.DropUserAsync(name).ConfigureAwait(false);
        }

        [Fact]
        public async Task Test_CreateAndDropUserWithScope()
        {
            var name = "usermgr_scope_role_test";
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            await cluster.Users.UpsertUserAsync(new User(name)
            {
                Password = "password",
                Roles = new List<Role>
                {
                    new Role("data_reader", "default", "_default")
                }
            }).ConfigureAwait(false);

            await cluster.Users.DropUserAsync(name).ConfigureAwait(false);
        }

        [Fact]
        public async Task Test_CreateAndDropUserWithCollection()
        {
            var name = "usermgr_collection_role_test";
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            await cluster.Users.UpsertUserAsync(new User(name)
            {
                Password = "password",
                Roles = new List<Role>
                {
                    new Role("data_reader", "default", "_default", "_default")
                }
            }).ConfigureAwait(false);

            await cluster.Users.DropUserAsync(name).ConfigureAwait(false);
        }
    }
}
