using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.Utils;
using Couchbase.Management.Users;
using Couchbase.Test.Common;
using Xunit;

namespace Couchbase.IntegrationTests.Management
{
    [Collection(NonParallelDefinition.Name)]
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
                Password = "password",
                Roles = new List<Role>()
                {
                    new Role("data_reader", "default")
                }
            }).ConfigureAwait(false);

            await cluster.Users.DropUserAsync("usermgr_test").ConfigureAwait(false);
        }

        [Fact]
        public async Task Test_CreateAndDropUserWithRoleNamesOnly()
        {
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);

            await cluster.Users.UpsertUserAsync(new User("usermgr_test") {
                Password = "password",
                Roles = new List<Role>()
                {
                    new Role("admin"),
                    new Role("ro_admin"),
                    new Role("cluster_admin")
                }
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

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
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

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
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

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_CanAssignCollectionsAwareRoles()
        {
            var name = "usermgr_collection_role_test1";
            var cluster = await _fixture.GetCluster().ConfigureAwait(false);
            await cluster.Users.UpsertUserAsync(new User(name)
            {
                Password = "password",
                Roles = new List<Role>
                {
                    new Role("data_reader", "default"),
                    new Role("data_reader", "default", "_default", null),
                    new Role("data_reader", "default", "_default", "_default")
                }
            }).ConfigureAwait(false);

            await cluster.Users.DropUserAsync(name).ConfigureAwait(false);
        }
    }
}
