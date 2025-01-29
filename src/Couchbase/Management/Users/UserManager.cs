using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Management.Users
{
    internal class UserManager : IUserManager
    {
        private readonly IServiceUriProvider _serviceUriProvider;
        private readonly ICouchbaseHttpClientFactory _httpClientFactory;
        private readonly ILogger<UserManager> _logger;
        private readonly IRedactor _redactor;
        private readonly IAppTelemetryCollector _appTelemetryCollector;

        public UserManager(IServiceUriProvider serviceUriProvider, ICouchbaseHttpClientFactory httpClientFactory,
            ILogger<UserManager> logger, IRedactor redactor, IAppTelemetryCollector appTelemetryCollector)
        {
            _serviceUriProvider = serviceUriProvider ?? throw new ArgumentNullException(nameof(serviceUriProvider));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _appTelemetryCollector =
                appTelemetryCollector ?? throw new ArgumentNullException(nameof(appTelemetryCollector));
        }

        private (IClusterNode, Uri) GetUsersUri(string domain, string? username = null)
        {
            var managementNode = _serviceUriProvider.GetRandomManagementNode();
            var builder = new UriBuilder(managementNode.ManagementUri)
            {
                Path = $"settings/rbac/users/{domain}"
            };

            if (!string.IsNullOrWhiteSpace(username))
            {
                builder.Path += $"/{username}";
            }

            return (managementNode, builder.Uri);
        }

        private (IClusterNode, Uri) GetRolesUri()
        {
            var managementNode = _serviceUriProvider.GetRandomManagementNode();
            var builder = new UriBuilder(managementNode.ManagementUri)
            {
                Path = "settings/rbac/roles"
            };

            return (managementNode, builder.Uri);
        }

        private (IClusterNode, Uri) GetGroupsUri(string? groupName = null)
        {
            var managementNode = _serviceUriProvider.GetRandomManagementNode();
            var builder = new UriBuilder(managementNode.ManagementUri)
            {
                Path = "settings/rbac/groups"
            };

            if (!string.IsNullOrWhiteSpace(groupName))
            {
                builder.Path += $"/{groupName}";
            }

            return (managementNode, builder.Uri);
        }

        private static IEnumerable<KeyValuePair<string, string?>> GetGroupFormValues(Group group)
        {
            return new Dictionary<string, string?>
            {
                { "description", group.Description },
                { "ldap_group_ref", group.LdapGroupReference },
                {
                    "roles", string.Join(",", group.Roles?.Select(
                            role => string.IsNullOrWhiteSpace(role.Bucket)
                                ? role.Name
                                : $"{role.Name}[{role.Bucket}]") ?? Enumerable.Empty<string>()
                    )
                }
            };
        }

        public async Task<UserAndMetaData> GetUserAsync(string username, GetUserOptions? options = null)
        {
            options ??= GetUserOptions.Default;
            var (mgmtNode, uri) = GetUsersUri(options.DomainNameValue, username);
            _logger.LogInformation("Attempting to get user with username {Username} - {Uri}",
                _redactor.UserData(username), _redactor.SystemData(uri));
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            try
            {
                // check user exists before trying to read content
                using var httpClient = _httpClientFactory.Create();
                requestStopwatch?.Restart();
                var result = await httpClient.GetAsync(uri, cts.FallbackToToken(options.TokenValue))
                    .ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new UserNotFoundException(username);
                }

                result.EnsureSuccessStatusCode();

                _appTelemetryCollector.IncrementMetrics(
                    operationElapsed,
                    mgmtNode.NodesAdapter.CanonicalHostname,
                    mgmtNode.NodesAdapter.AlternateHostname,
                    mgmtNode.NodeUuid,
                    AppTelemetryServiceType.Management,
                    AppTelemetryCounterType.Total);

                // get user from result
                using var stream = await result.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var dto = (await JsonSerializer
                    .DeserializeAsync(stream, UserManagementSerializerContext.Default.UserAndMetadataDto)
                    .ConfigureAwait(false))!;

                return UserAndMetaData.FromJson(dto);
            }
            catch (Exception exception)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
                _logger.LogError(exception, "Error trying to get user with username {Username} - {Uri}",
                    _redactor.UserData(username), _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task<IEnumerable<UserAndMetaData>> GetAllUsersAsync(GetAllUsersOptions? options = null)
        {
            options ??= GetAllUsersOptions.Default;
            var (mgmtNode, uri) = GetUsersUri(options.DomainNameValue);
            _logger.LogInformation("Attempting to get all users - {Uri}", _redactor.SystemData(uri));
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            try
            {
                // get all users
                using var httpClient = _httpClientFactory.Create();
                requestStopwatch?.Restart();
                var result = await httpClient.GetAsync(uri, cts.FallbackToToken(options.TokenValue))
                    .ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;
                result.EnsureSuccessStatusCode();

                _appTelemetryCollector.IncrementMetrics(
                    operationElapsed,
                    mgmtNode.NodesAdapter.CanonicalHostname,
                    mgmtNode.NodesAdapter.AlternateHostname,
                    mgmtNode.NodeUuid,
                    AppTelemetryServiceType.Management,
                    AppTelemetryCounterType.Total);

                // get users from result
                using var stream = await result.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var dtos = (await JsonSerializer
                    .DeserializeAsync(stream, UserManagementSerializerContext.Default.ListUserAndMetadataDto)
                    .ConfigureAwait(false))!;

                return dtos.Select(UserAndMetaData.FromJson);
            }
            catch (Exception exception)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
                _logger.LogError(exception, "Error trying to get all users - {Uri}", _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task UpsertUserAsync(User user, UpsertUserOptions? options = null)
        {
            options ??= UpsertUserOptions.Default;
            var (mgmtNode, uri) = GetUsersUri(options.DomainNameValue, user.Username);
            _logger.LogInformation("Attempting to create user with username {Username} - {Uri}",
                _redactor.UserData(user.Username), _redactor.SystemData(uri));
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            try
            {
                // upsert user
                var content = new FormUrlEncodedContent(user.GetUserFormValues());
                using var httpClient = _httpClientFactory.Create();
                requestStopwatch?.Restart();
                var result = await httpClient.PutAsync(uri, content, cts.FallbackToToken(options.TokenValue))
                    .ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;
                result.EnsureSuccessStatusCode();
                _appTelemetryCollector.IncrementMetrics(
                    operationElapsed,
                    mgmtNode.NodesAdapter.CanonicalHostname,
                    mgmtNode.NodesAdapter.AlternateHostname,
                    mgmtNode.NodeUuid,
                    AppTelemetryServiceType.Management,
                    AppTelemetryCounterType.Total);

            }
            catch (Exception exception)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
                _logger.LogError(exception, "Error trying to upsert user - {Uri}", _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task DropUserAsync(string username, DropUserOptions? options = null)
        {
            options ??= DropUserOptions.Default;
            var (mgmtNode, uri) = GetUsersUri(options.DomainNameValue, username);
            _logger.LogInformation("Attempting to drop user with username {Username} - {Uri}",
                _redactor.UserData(username), _redactor.SystemData(uri));
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            try
            {
                // drop user
                using var httpClient = _httpClientFactory.Create();
                requestStopwatch?.Restart();
                var result = await httpClient.DeleteAsync(uri, cts.FallbackToToken(options.TokenValue))
                    .ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new UserNotFoundException(username);
                }

                result.EnsureSuccessStatusCode();
                _appTelemetryCollector.IncrementMetrics(
                    operationElapsed,
                    mgmtNode.NodesAdapter.CanonicalHostname,
                    mgmtNode.NodesAdapter.AlternateHostname,
                    mgmtNode.NodeUuid,
                    AppTelemetryServiceType.Management,
                    AppTelemetryCounterType.Total);

            }
            catch (Exception exception)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
                _logger.LogError(exception, "Error trying to drop user with username {Username} - {Uri}",
                    _redactor.UserData(username), _redactor.SystemData(uri));
                throw;
            }
        }

        private IEnumerable<KeyValuePair<string, string>> FormatPassword(string newPassword)
        {
            return new Dictionary<string, string> { { "password", newPassword } };
        }

        public async Task ChangeUserPasswordAsync(string newPassword, ChangePasswordOptions? options = null)
        {
            options ??= ChangePasswordOptions.Default;
            var mgmtNode = _serviceUriProvider.GetRandomManagementNode();
            var builder = new UriBuilder(mgmtNode.ManagementUri)
            {
                Path = "controller/changePassword"
            };

            var content = new FormUrlEncodedContent(FormatPassword(newPassword)!);

            _logger.LogInformation("Attempting to change current user password");
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            try
            {
                using var httpClient = _httpClientFactory.Create();
                requestStopwatch?.Restart();
                var result = await httpClient.PostAsync(builder.Uri, content, cts.FallbackToToken(options.TokenValue))
                    .ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;

                if (!result.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Error when attempting to change user password. HTTP Error: {(int)result.StatusCode} - {result.StatusCode}.");
                }

                _appTelemetryCollector.IncrementMetrics(
                    operationElapsed,
                    mgmtNode.NodesAdapter.CanonicalHostname,
                    mgmtNode.NodesAdapter.AlternateHostname,
                    mgmtNode.NodeUuid,
                    AppTelemetryServiceType.Management,
                    AppTelemetryCounterType.Total);
            }
            catch (Exception exception)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
                _logger.LogError(exception, "Unknown error while attempting to change user password");
                throw;
            }
        }

        public async Task<IEnumerable<RoleAndDescription>> GetRolesAsync(AvailableRolesOptions? options = null)
        {
            options ??= AvailableRolesOptions.Default;
            var (mgmtNode, uri) = GetRolesUri();
            _logger.LogInformation("Attempting to get all available roles - {Uri}", _redactor.MetaData(uri));
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            try
            {
                // get roles
                using var httpClient = _httpClientFactory.Create();
                requestStopwatch?.Restart();
                var result = await httpClient.GetAsync(uri, cts.FallbackToToken(options.TokenValue))
                    .ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;
                result.EnsureSuccessStatusCode();

                _appTelemetryCollector.IncrementMetrics(
                    operationElapsed,
                    mgmtNode.NodesAdapter.CanonicalHostname,
                    mgmtNode.NodesAdapter.AlternateHostname,
                    mgmtNode.NodeUuid,
                    AppTelemetryServiceType.Management,
                    AppTelemetryCounterType.Total);

                // get roles from result
                using var stream = await result.Content.ReadAsStreamAsync().ConfigureAwait(false);
                return (await JsonSerializer
                    .DeserializeAsync(stream, UserManagementSerializerContext.Default.ListRoleAndDescription)
                    .ConfigureAwait(false))!;
            }
            catch (Exception exception)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
                _logger.LogError(exception, "Error trying to get all available roles - {Uri}",
                    _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task<Group> GetGroupAsync(string groupName, GetGroupOptions? options = null)
        {
            options ??= GetGroupOptions.Default;
            var (mgmtNode, uri) = GetGroupsUri(groupName);
            _logger.LogInformation("Attempting to get group with name {GroupName} - {Uri}",
                _redactor.UserData(groupName),
                _redactor.SystemData(uri));
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            try
            {
                // get group
                using var httpClient = _httpClientFactory.Create();
                requestStopwatch?.Restart();
                var result = await httpClient.GetAsync(uri, cts.FallbackToToken(options.TokenValue))
                    .ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new GroupNotFoundException(groupName);
                }

                result.EnsureSuccessStatusCode();

                _appTelemetryCollector.IncrementMetrics(
                    operationElapsed,
                    mgmtNode.NodesAdapter.CanonicalHostname,
                    mgmtNode.NodesAdapter.AlternateHostname,
                    mgmtNode.NodeUuid,
                    AppTelemetryServiceType.Management,
                    AppTelemetryCounterType.Total);

                // get group from result
                using var stream = await result.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var dto = (await JsonSerializer
                    .DeserializeAsync(stream, UserManagementSerializerContext.Default.GroupDto)
                    .ConfigureAwait(false))!;

                return Group.FromJson(dto);
            }
            catch (Exception exception)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
                _logger.LogError(exception, "Error trying to get group with name {GroupName} - {Uri}",
                    _redactor.UserData(groupName), _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task<IEnumerable<Group>> GetAllGroupsAsync(GetAllGroupsOptions? options = null)
        {
            options ??= GetAllGroupsOptions.Default;
            var (mgmtNode, uri) = GetGroupsUri();
            _logger.LogInformation("Attempting to get all groups - {Uri}", _redactor.SystemData(uri));
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            try
            {
                // get group
                using var httpClient = _httpClientFactory.Create();
                requestStopwatch?.Restart();
                var result = await httpClient.GetAsync(uri, cts.FallbackToToken(options.TokenValue))
                    .ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;
                result.EnsureSuccessStatusCode();

                _appTelemetryCollector.IncrementMetrics(
                    operationElapsed,
                    mgmtNode.NodesAdapter.CanonicalHostname,
                    mgmtNode.NodesAdapter.AlternateHostname,
                    mgmtNode.NodeUuid,
                    AppTelemetryServiceType.Management,
                    AppTelemetryCounterType.Total);

                // get groups from results
                using var stream = await result.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var dtos = (await JsonSerializer
                    .DeserializeAsync(stream, UserManagementSerializerContext.Default.ListGroupDto)
                    .ConfigureAwait(false))!;

                return dtos.Select(Group.FromJson);
            }
            catch (Exception exception)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
                _logger.LogError(exception, "Error trying to get all groups - {Uri}", _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task UpsertGroupAsync(Group group, UpsertGroupOptions? options = null)
        {
            options ??= UpsertGroupOptions.Default;
            var (mgmtNode, uri) = GetGroupsUri(group.Name);
            _logger.LogInformation("Attempting to upsert group with name {Name} - {Uri}",
                _redactor.UserData(group.Name), _redactor.SystemData(uri));
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            try
            {
                // upsert group
                var content = new FormUrlEncodedContent(GetGroupFormValues(group)!);
                using var httpClient = _httpClientFactory.Create();
                requestStopwatch?.Restart();
                var result = await httpClient.PutAsync(uri, content, cts.FallbackToToken(options.TokenValue))
                    .ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;
                result.EnsureSuccessStatusCode();
                _appTelemetryCollector.IncrementMetrics(
                    operationElapsed,
                    mgmtNode.NodesAdapter.CanonicalHostname,
                    mgmtNode.NodesAdapter.AlternateHostname,
                    mgmtNode.NodeUuid,
                    AppTelemetryServiceType.Management,
                    AppTelemetryCounterType.Total);
            }
            catch (Exception exception)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
                _logger.LogError(exception, "Error trying to upsert group with name {GroupName} - {Uri}",
                    _redactor.UserData(group.Name), _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task DropGroupAsync(string groupName, DropGroupOptions? options = null)
        {
            options ??= DropGroupOptions.Default;
            var (mgmtNode, uri) = GetGroupsUri(groupName);
            _logger.LogInformation("Attempting to drop group with name {GroupName} - {Uri}",
                _redactor.UserData(groupName), _redactor.SystemData(uri));
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            try
            {
                // drop group
                using var httpClient = _httpClientFactory.Create();
                requestStopwatch?.Restart();
                var result = await httpClient.DeleteAsync(uri, cts.FallbackToToken(options.TokenValue))
                    .ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new GroupNotFoundException(groupName);
                }

                result.EnsureSuccessStatusCode();
                _appTelemetryCollector.IncrementMetrics(
                    operationElapsed,
                    mgmtNode.NodesAdapter.CanonicalHostname,
                    mgmtNode.NodesAdapter.AlternateHostname,
                    mgmtNode.NodeUuid,
                    AppTelemetryServiceType.Management,
                    AppTelemetryCounterType.Total);
            }
            catch (Exception exception)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
                _logger.LogError(exception, "Error trying to drop group with name {GroupName} - {Uri}",
                    _redactor.UserData(groupName), _redactor.SystemData(uri));
                throw;
            }
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
