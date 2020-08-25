using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

#nullable enable

namespace Couchbase.Management.Users
{
    internal class UserManager : IUserManager
    {
        private readonly IServiceUriProvider _serviceUriProvider;
        private readonly CouchbaseHttpClient _client;
        private readonly ILogger<UserManager> _logger;
        private readonly IRedactor _redactor;

        public UserManager(IServiceUriProvider serviceUriProvider, CouchbaseHttpClient httpClient,
            ILogger<UserManager> logger, IRedactor redactor)
        {
            _serviceUriProvider = serviceUriProvider ?? throw new ArgumentNullException(nameof(serviceUriProvider));
            _client = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        }

        private Uri GetUsersUri(string domain, string? username = null)
        {
            var builder = new UriBuilder(_serviceUriProvider.GetRandomManagementUri())
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
            var builder = new UriBuilder(_serviceUriProvider.GetRandomManagementUri())
            {
                Path = "settings/rbac/roles"
            };

            return builder.Uri;
        }

        private Uri GetGroupsUri(string? groupName = null)
        {
            var builder = new UriBuilder(_serviceUriProvider.GetRandomManagementUri())
            {
                Path = "settings/rbac/groups"
            };

            if (!string.IsNullOrWhiteSpace(groupName))
            {
                builder.Path += $"/{groupName}";
            }

            return builder.Uri;
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

        public async Task<UserAndMetaData> GetUserAsync(string username, GetUserOptions? options = null)
        {
            options ??= GetUserOptions.Default;
            var uri = GetUsersUri(options.DomainNameValue, username);
            _logger.LogInformation("Attempting to get user with username {username} - {uri}",
                _redactor.UserData(username), _redactor.SystemData(uri));

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
                _logger.LogError(exception, "Error trying to get user with username {username} - {uri}",
                    _redactor.UserData(username), _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task<IEnumerable<UserAndMetaData>> GetAllUsersAsync(GetAllUsersOptions? options = null)
        {
            options ??= GetAllUsersOptions.Default;
            var uri = GetUsersUri(options.DomainNameValue);
            _logger.LogInformation("Attempting to get all users - {uri}", _redactor.SystemData(uri));

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
                _logger.LogError(exception, "Error trying to get all users - {uri}", _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task UpsertUserAsync(User user, UpsertUserOptions? options = null)
        {
            options ??= UpsertUserOptions.Default;
            var uri = GetUsersUri(options.DomainNameValue, user.Username);
            _logger.LogInformation("Attempting to create user with username {user.Username} - {uri}",
                _redactor.UserData(user.Username), _redactor.SystemData(uri));

            try
            {
                // upsert user
                var content = new FormUrlEncodedContent(user.GetUserFormValues());
                var result = await _client.PutAsync(uri, content, options.TokenValue).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error trying to upsert user - {uri}", _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task DropUserAsync(string username, DropUserOptions? options = null)
        {
            options ??= DropUserOptions.Default;
            var uri = GetUsersUri(options.DomainNameValue, username);
            _logger.LogInformation("Attempting to drop user with username {username} - {uri}",
                _redactor.UserData(username), _redactor.SystemData(uri));

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
                _logger.LogError(exception, $"Error trying to drop user with username {username} - {uri}",
                    _redactor.UserData(username), _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task<IEnumerable<RoleAndDescription>> GetRolesAsync(AvailableRolesOptions? options = null)
        {
            options ??= AvailableRolesOptions.Default;
            var uri = GetRolesUri();
            _logger.LogInformation("Attempting to get all available roles - {uri}", _redactor.MetaData(uri));

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
                _logger.LogError(exception, "Error trying to get all available roles - {uri}", _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task<Group> GetGroupAsync(string groupName, GetGroupOptions? options = null)
        {
            options ??= GetGroupOptions.Default;
            var uri = GetGroupsUri(groupName);
            _logger.LogInformation("Attempting to get group with name {groupName} - {uri}", _redactor.UserData(groupName),
                _redactor.SystemData(uri));

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
                _logger.LogError(exception, "Error trying to get group with name {groupName} - {uri}",
                    _redactor.UserData(groupName), _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task<IEnumerable<Group>> GetAllGroupsAsync(GetAllGroupsOptions? options = null)
        {
            options ??= GetAllGroupsOptions.Default;
            var uri = GetGroupsUri();
            _logger.LogInformation("Attempting to get all groups - {uri}", _redactor.SystemData(uri));

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
                _logger.LogError(exception, "Error trying to get all groups - {uri}", _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task UpsertGroupAsync(Group group, UpsertGroupOptions? options = null)
        {
            options ??= UpsertGroupOptions.Default;
            var uri = GetGroupsUri(group.Name);
            _logger.LogInformation("Attempting to upsert group with name {group.Name} - {uri}",
                _redactor.UserData(group.Name), _redactor.SystemData(uri));

            try
            {
                // upsert group
                var content = new FormUrlEncodedContent(GetGroupFormValues(group));
                var result = await _client.PutAsync(uri, content, options.TokenValue).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error trying to upsert group with name {group.Name} - {uri}",
                    _redactor.UserData(group.Name), _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task DropGroupAsync(string groupName, DropGroupOptions? options = null)
        {
            options ??= DropGroupOptions.Default;
            var uri = GetGroupsUri(groupName);
            _logger.LogInformation($"Attempting to drop group with name {groupName} - {uri}",
                _redactor.UserData(groupName), _redactor.SystemData(uri));

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
                _logger.LogError(exception, "Error trying to drop group with name {groupName} - {uri}",
                    _redactor.UserData(groupName), _redactor.SystemData(uri));
                throw;
            }
        }
    }
}
