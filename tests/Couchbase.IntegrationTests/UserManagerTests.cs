using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.Management;
using Couchbase.Management.Users;
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

            const string username = "test_user", displayName = "Test User", password = "pa$$w0rd", groupName = "test_group";
            var roles = new List<Role>
            {
                new Role {Name = "bucket_admin", Bucket = "default"}
            };

            try
            {
                // available roles
                var availableRoles = await userManager.GetRolesAsync();
                Assert.Contains(availableRoles, role => role.Role.Name == "admin");

                // upsert group
                var group = new Group(groupName)
                {
                    Description = "test_description",
                    LdapGroupReference = "asda=price",
                    Roles = new[] {new Role {Name = "admin"}, new Role{Name = "bucket_admin", Bucket = "*"}}
                };
                await userManager.UpsertGroupAsync(group);

                // get group
                var groupResult = await userManager.GetGroupAsync(groupName);
                Assert.Equal(group.Name, groupResult.Name);
                Assert.Equal(group.Description, groupResult.Description);
                Assert.Equal(group.LdapGroupReference, groupResult.LdapGroupReference);
                Assert.Contains(group.Roles, role => role.Name == "admin");
                Assert.Contains(group.Roles, role => role.Name == "bucket_admin" && role.Bucket == "*");

                // get all groups
                var allGroupsResult = await userManager.GetAllGroupsAsync();
                groupResult = allGroupsResult.Single(x => x.Name == groupName);
                Assert.Equal(group.Name, groupResult.Name);
                Assert.Equal(group.Description, groupResult.Description);
                Assert.Equal(group.LdapGroupReference, groupResult.LdapGroupReference);
                Assert.Contains(group.Roles, role => role.Name == "admin");
                Assert.Contains(group.Roles, role => role.Name == "bucket_admin" && role.Bucket == "*");

                // upsert user
                var user = new User(username)
                {
                    DisplayName = displayName,
                    Groups = new [] {groupName},
                    Roles = roles,
                    Password = password
                };
                await userManager.UpsertUsersAsync(user);

                // get user with meta
                var userResult = await userManager.GetUserAsync(username);
                Assert.Equal(username, userResult.Username);
                Assert.Equal(displayName, userResult.DisplayName);
                Assert.Equal("local", userResult.Domain);
                Assert.NotEqual(default, userResult.PasswordChanged);
                Assert.Contains(userResult.EffectiveRoles, role => role.Name == "admin");
                Assert.Contains(userResult.EffectiveRoles, role => role.Name == "bucket_admin" && role.Bucket == "*");
                Assert.Contains(userResult.Groups, x => x == groupName);

                // get all users with meta
                var users = await userManager.GetAllUsersAsync();
                userResult = users.Single(x => x.Username == username);
                Assert.Equal(username, userResult.Username);
                Assert.Equal(displayName, userResult.DisplayName);
                Assert.Equal("local", userResult.Domain);
                Assert.NotEqual(default, userResult.PasswordChanged);
                Assert.Contains(userResult.EffectiveRoles, role => role.Name == "admin");
                Assert.Contains(userResult.EffectiveRoles, role => role.Name == "bucket_admin" && role.Bucket == "*");
                Assert.Contains(userResult.Groups, x => x == groupName);
            }
            finally
            {
                // drop user
                await userManager.DropUserAsync(username);

                // drop group
                await userManager.DropGroupAsync(groupName);
            }
        }
    }
}
