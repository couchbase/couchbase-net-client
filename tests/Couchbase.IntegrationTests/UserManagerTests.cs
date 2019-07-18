using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Management;
using Xunit;

namespace Couchbase.IntegrationTests
{
    public class UserManagerTests : IClassFixture<ClusterFixture>
    {
        private readonly ClusterFixture _fixture;

        public UserManagerTests(ClusterFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Test_UserManager()
        {
            var userManager = _fixture.Cluster.Users;

            const string username = "test_user", password = "pa$$w0rd";
            var roles = new List<UserRole>
            {
                new UserRole {Name = "bucket_admin", BucketName = "default"}
            };

            try
            {
                // create
                await userManager.CreateAsync(username, password, roles);

                // upsert
                await userManager.UpsertAsync(username, roles);

                // get
                var user = await userManager.GetAsync(username);
                Assert.Equal(username, user.Username);
                Assert.Equal("local", user.Domain);
                Assert.Single(user.Roles);
                Assert.Equal(user.Roles.First().Name, roles.First().Name);
                Assert.Equal(user.Roles.First().BucketName, roles.First().BucketName);

                // get all
                var users = await userManager.GetAllAsync();
                user = users.Single(x => x.Name == username);
                Assert.Equal(username, user.Username);
                Assert.Equal("local", user.Domain);
                Assert.Single(user.Roles);
                Assert.Equal(user.Roles.First().Name, roles.First().Name);
                Assert.Equal(user.Roles.First().BucketName, roles.First().BucketName);
            }
            finally
            {
                // drop
                await userManager.DropAsync(username);
            }
        }
    }
}
