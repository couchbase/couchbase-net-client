using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.IntegrationTests.Fixtures;
using Couchbase.IntegrationTests.Utils;
using Couchbase.Management;
using Couchbase.Management.Users;
using Couchbase.Query;
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

        [CouchbaseVersionDependentFact(MinVersion = "6.5.0")]
        public async Task Test_UserManager()
        {
            var userManager = _fixture.Cluster.Users;

            const string username = "test_user", displayName = "Test User", password = "pa$$w0rd", groupName = "test_group";
            var roles = new List<Role>
            {
                new Role("bucket_admin","default")
            };

            try
            {
                // available roles
                var availableRoles = await userManager.GetRolesAsync().ConfigureAwait(false);
                Assert.Contains(availableRoles, role => role.Role == "admin");

                // upsert group
                var group = new Group(groupName)
                {
                    Description = "test_description",
                    LdapGroupReference = "asda=price",
                    Roles = new[] {new Role("admin"), new Role("bucket_admin", "*")}
                };
                await userManager.UpsertGroupAsync(@group).ConfigureAwait(false);

                // get group
                var groupResult = await userManager.GetGroupAsync(groupName).ConfigureAwait(false);
                Assert.Equal(group.Name, groupResult.Name);
                Assert.Equal(group.Description, groupResult.Description);
                Assert.Equal(group.LdapGroupReference, groupResult.LdapGroupReference);
                Assert.Contains(group.Roles, role => role.Name == "admin");
                Assert.Contains(group.Roles, role => role.Name == "bucket_admin" && role.Bucket == "*");

                // get all groups
                var allGroupsResult = await userManager.GetAllGroupsAsync().ConfigureAwait(false);
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
                    Groups = new[] {groupName},
                    Roles = roles,
                    Password = password
                };
                await userManager.UpsertUsersAsync(user).ConfigureAwait(false);

                // get user with meta
                var userResult = await userManager.GetUserAsync(username).ConfigureAwait(false);
                Assert.Equal(username, userResult.Username);
                Assert.Equal(displayName, userResult.DisplayName);
                Assert.Equal("local", userResult.Domain);
                Assert.NotEqual(default, userResult.PasswordChanged);
                Assert.Contains(userResult.EffectiveRoles, role => role.Name == "bucket_admin" && role.Bucket == "default");
                Assert.Contains(userResult.Groups, x => x == groupName);

                // get all users with meta
                var users = await userManager.GetAllUsersAsync().ConfigureAwait(false);
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
                await userManager.DropUserAsync(username).ConfigureAwait(false);

                // drop group
                await userManager.DropGroupAsync(groupName).ConfigureAwait(false);
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "7.0.0")]
        public async Task Test_UserInheritsCollectionAwareRoles()
        {
            var userManager = _fixture.Cluster.Users;
            const string groupName = "test_group1", username = "test_user1";

            try
            {
                var group = new Group(groupName)
                {
                    Description = "test_description",
                    LdapGroupReference = "asda=price",
                    Roles = new[] {
                    new Role("admin"), new Role("bucket_admin", "*"),
                    new Role("data_reader", "default", "_default", null),
                    new Role("data_reader", "default", "_default", "_default")
                }};
                await userManager.UpsertGroupAsync(@group).ConfigureAwait(false);
                var groupResult = await userManager.GetGroupAsync(groupName).ConfigureAwait(false);

                var user = new User(username)
                {
                    DisplayName = nameof(Test_UserInheritsCollectionAwareRoles),
                    Groups = new[] { groupName },
                    Password = nameof(Test_UserInheritsCollectionAwareRoles)
                };

                await userManager.UpsertUserAsync(user);

                Assert.Equal(group.Name, groupResult.Name);
                Assert.Equal(group.Description, groupResult.Description);
            }
            finally
            {
                // drop user
                await userManager.DropUserAsync(username).ConfigureAwait(false);

                // drop group
                await userManager.DropGroupAsync(groupName).ConfigureAwait(false);
            }
        }

        [CouchbaseVersionDependentFact(MinVersion = "6.0.0")]
        public async Task Test_ChangeUserPassword()
        {
            var globalUserManager = _fixture.Cluster.Users;

            const string username = "username", originalPassword = "password", newPassword = "newPassword", groupName = "groupName";

            var group = new Group(groupName)
            {
                Roles = new[]
                {
                    new Role("admin")
                }};

            var user = new User(username)
            {
                Password = originalPassword,
                Groups = new[] { groupName }
            };

            await globalUserManager.UpsertGroupAsync(@group).ConfigureAwait(false);
            await globalUserManager.UpsertUsersAsync(user).ConfigureAwait(false);

            try
            {
                var disposableConnection =
                    await NetClient.Cluster.ConnectAsync(_fixture.ClusterOptions.ConnectionString, username, originalPassword).ConfigureAwait(false);
                var disposableUserManager = disposableConnection.Users;
                await disposableUserManager.ChangeUserPasswordAsync(newPassword).ConfigureAwait(false);

                var exception = await Record.ExceptionAsync(() => NetClient.Cluster.ConnectAsync(_fixture.ClusterOptions.ConnectionString, username, newPassword));
                Assert.Null(exception);

                await Assert.ThrowsAsync<AuthenticationFailureException>( () => NetClient.Cluster.ConnectAsync(_fixture.ClusterOptions.ConnectionString, username, originalPassword));
            }
            finally
            {
                await globalUserManager.DropUserAsync(username).ConfigureAwait(false);
                await globalUserManager.DropGroupAsync(groupName).ConfigureAwait(false);
            }

        }
    }
}
