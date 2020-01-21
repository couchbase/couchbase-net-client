using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Couchbase.Management.Users
{
    internal class UserManager : IUserManager
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<UserManager>();
        private readonly HttpClient _client;
        private readonly ClusterContext _context;

        public UserManager(ClusterContext context)
        {
            _context = context;
            _client = new HttpClient(new AuthenticatingHttpClientHandler(_context));
        }

        private Uri GetUsersUri(string domain, string username = null)
        {
            var builder = new UriBuilder(_context.GetRandomNode().ManagementUri)
            {
                Path = $"settings/rbac/users/{domain}"
            };

            if (!string.IsNullOrWhiteSpace(username))
            {
                builder.Path += $"/{username}";
            }

            return builder.Uri;
        }

        private Uri GetRolesUri()
        {
            var builder = new UriBuilder(_context.GetRandomNode().ManagementUri)
            {
                Path = "settings/rbac/roles"
            };

            return builder.Uri;
        }

        private Uri GetGroupsUri(string groupName = null)
        {
            var builder = new UriBuilder(_context.GetRandomNode().ManagementUri)
            {
                Path = "settings/rbac/groups"
            };

            if (!string.IsNullOrWhiteSpace(groupName))
            {
                builder.Path += $"/{groupName}";
            }

            return builder.Uri;
        }

        private static IEnumerable<KeyValuePair<string, string>> GetUserFormValues(User user)
        {
            var values = new Dictionary<string, string>();

            if (!string.IsNullOrWhiteSpace(user.DisplayName))
            {
                values.Add("name", user.DisplayName);
            }

            if (!string.IsNullOrWhiteSpace(user.Password))
            {
                values.Add("password", user.Password);
            }

            if (user.Roles?.Any() ?? false)
            {
                values.Add("roles", string.Join(",",
                    user.Roles.Select(role => string.IsNullOrWhiteSpace(role.Bucket)
                        ? role.Name
                        : $"{role.Name}[{role.Bucket}]")
                    )
                );
            }

            if (user.Groups?.Any() ?? false)
            {
                values.Add("groups", string.Join(",", user.Groups));
            }

            return values;
        }

        private static IEnumerable<KeyValuePair<string, string>> GetGroupFormValues(Group group)
        {
            return new Dictionary<string, string>
            {
                {"description", group.Description},
                {"ldap_group_ref", group.LdapGroupReference},
                {
                    "roles", string.Join(",", group.Roles.Select(
                        role => string.IsNullOrWhiteSpace(role.Bucket)
                            ? role.Name
                            : $"{role.Name}[{role.Bucket}]")
                    )
                }
            };
        }

        public async Task<UserAndMetaData> GetUserAsync(string username, GetUserOptions options = null)
        {
            options = options ?? GetUserOptions.Default;
            var uri = GetUsersUri(options.DomainNameValue, username);
            Logger.LogInformation($"Attempting to get user with username {username} - {uri}");

            try
            {
                // check user exists before trying to read content
                var result = await _client.GetAsync(uri, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new UserNotFoundException(username);
                }

                result.EnsureSuccessStatusCode();

                // get user from result
                var json = JObject.Parse(await result.Content.ReadAsStringAsync().ConfigureAwait(false));
                return UserAndMetaData.FromJson(json);
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Error trying to get user with username {username} - {uri}");
                throw;
            }
        }

        public async Task<IEnumerable<UserAndMetaData>> GetAllUsersAsync(GetAllUsersOptions options = null)
        {
            options = options ?? GetAllUsersOptions.Default;
            var uri = GetUsersUri(options.DomainNameValue);
            Logger.LogInformation($"Attempting to get all users - {uri}");

            try
            {
                // get all users
                var result = await _client.GetAsync(uri, options.TokenValue).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();

                // get users from result
                var json = JArray.Parse(await result.Content.ReadAsStringAsync().ConfigureAwait(false));
                return json.Select(UserAndMetaData.FromJson);
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Error trying to get all users - {uri}");
                throw;
            }
        }

        public async Task UpsertUserAsync(User user, UpsertUserOptions options = null)
        {
            options = options ?? UpsertUserOptions.Default;
            var uri = GetUsersUri(options.DomainNameValue, user.Username);
            Logger.LogInformation($"Attempting to create user with username {user.Username} - {uri}");

            try
            {
                // upsert user
                var content = new FormUrlEncodedContent(GetUserFormValues(user));
                var result = await _client.PutAsync(uri, content, options.TokenValue).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Error trying to upsert user - {uri}");
                throw;
            }
        }

        public async Task DropUserAsync(string username, DropUserOptions options = null)
        {
            options = options ?? DropUserOptions.Default;
            var uri = GetUsersUri(options.DomainNameValue, username);
            Logger.LogInformation($"Attempting to drop user with username {username} - {uri}");

            try
            {
                // drop user
                var result = await _client.DeleteAsync(uri, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new UserNotFoundException(username);
                }

                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Error trying to drop user with username {username} - {uri}");
                throw;
            }
        }

        public async Task<IEnumerable<RoleAndDescription>> AvailableRolesAsync(AvailableRolesOptions options = null)
        {
            options = options ?? AvailableRolesOptions.Default;
            var uri = GetRolesUri();
            Logger.LogInformation($"Attempting to get all available roles - {uri}");

            try
            {
                // get roles
                var result = await _client.GetAsync(uri, options.TokenValue).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();

                // get roles from result
                var json = JArray.Parse(await result.Content.ReadAsStringAsync().ConfigureAwait(false));
                return json.Select(RoleAndDescription.FromJson);
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Error trying to get all available roles - {uri}");
                throw;
            }
        }

        public async Task<Group> GetGroupAsync(string groupName, GetGroupOptions options = null)
        {
            options = options ?? GetGroupOptions.Default;
            var uri = GetGroupsUri(groupName);
            Logger.LogInformation($"Attempting to get group with name {groupName} - {uri}");

            try
            {
                // get group
                var result = await _client.GetAsync(uri, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new GroupNotFoundException(groupName);
                }

                result.EnsureSuccessStatusCode();

                // get group from result
                var json = JObject.Parse(await result.Content.ReadAsStringAsync().ConfigureAwait(false));
                return Group.FromJson(json);
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Error trying to get group with name {groupName} - {uri}");
                throw;
            }
        }

        public async Task<IEnumerable<Group>> GetAllGroupsAsync(GetAllGroupsOptions options = null)
        {
            options = options ?? GetAllGroupsOptions.Default;
            var uri = GetGroupsUri();
            Logger.LogInformation($"Attempting to get all groups - {uri}");

            try
            {
                // get group
                var result = await _client.GetAsync(uri, options.TokenValue).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();

                // get groups from results
                var json = JArray.Parse(await result.Content.ReadAsStringAsync().ConfigureAwait(false));
                return json.Select(Group.FromJson);
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Error trying to get all groups - {uri}");
                throw;
            }
        }

        public async Task UpsertGroupAsync(Group group, UpsertGroupOptions options = null)
        {
            options = options ?? UpsertGroupOptions.Default;
            var uri = GetGroupsUri(group.Name);
            Logger.LogInformation($"Attempting to upsert group with name {group.Name} - {uri}");

            try
            {
                // upsert group
                var content = new FormUrlEncodedContent(GetGroupFormValues(group));
                var result = await _client.PutAsync(uri, content, options.TokenValue).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Error trying to upsert group with name {group.Name} - {uri}");
                throw;
            }
        }

        public async Task DropGroupAsync(string groupName, DropGroupOptions options = null)
        {
            options = options ?? DropGroupOptions.Default;
            var uri = GetGroupsUri(groupName);
            Logger.LogInformation($"Attempting to drop group with name {groupName} - {uri}");

            try
            {
                // drop group
                var result = await _client.DeleteAsync(uri, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new GroupNotFoundException(groupName);
                }

                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Error trying to drop group with name {groupName} - {uri}");
                throw;
            }
        }
    }
}
