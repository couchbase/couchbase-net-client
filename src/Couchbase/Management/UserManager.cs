using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Couchbase.Management
{
    internal class UserManager : IUserManager
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<UserManager>();

        private readonly HttpClient _client;
        private readonly Configuration _configuration;

        public UserManager(Configuration configuration)
        {
            _configuration = configuration;
            _client = new HttpClient(new AuthenticatingHttpClientHandler(configuration.UserName, configuration.Password));
        }

        private Uri GetUserManagementUri(AuthenticationDomain domain, string username = null)
        {
            var builder = new UriBuilder
            {
                Scheme = _configuration.UseSsl ? "https" : "http",
                Host = _configuration.Servers.GetRandom().Host,
                Port = _configuration.UseSsl ? 18091 : 8091, //TODO: use configured ports
                Path = $"settings/rbac/users/{domain.GetDescription()}"
            };

            if (!string.IsNullOrWhiteSpace(username))
            {
                builder.Path += $"/{username}";
            }

            return builder.Uri;
        }

        private static IEnumerable<KeyValuePair<string, string>> GetUserFormValues(string password, string name, IEnumerable<UserRole> roles)
        {
            var rolesValue = string.Join(",",
                roles.Select(role => string.IsNullOrWhiteSpace(role.BucketName)
                    ? role.Name
                    : string.Format("{0}[{1}]", role.Name, role.BucketName))
            );

            var values = new Dictionary<string, string>
            {
                {"roles", rolesValue}
            };

            if (!string.IsNullOrWhiteSpace(password))
            {
                values.Add("password", password);
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                values.Add("name", name);
            }

            return values;
        }

        public async Task<User> GetAsync(string username, GetUserOptions options)
        {
            var uri = GetUserManagementUri(options.AuthenticationDomain, username);
            Logger.LogInformation($"Attempting to get user with username {username} - {uri}");

            try
            {
                // check user exists before trying to read content
                var result = await _client.GetAsync(uri, options.CancellationToken).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new UserNotFoundException(username);
                }

                result.EnsureSuccessStatusCode();

                var json = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<User>(json);
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Error trying to get user with username {username} - {uri}");
                throw;
            }
        }

        public async Task<IEnumerable<User>> GetAllAsync(GetAllUserOptions options)
        {
            var uri = GetUserManagementUri(options.AuthenticationDomain);
            Logger.LogInformation($"Attempting to get all users - {uri}");

            try
            {
                var result = await _client.GetAsync(uri, options.CancellationToken).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();

                var content = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<List<User>>(content);
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Error trying to get all users - {uri}");
                throw;
            }
        }

        public async Task CreateAsync(string username, string password, IEnumerable<UserRole> roles, CreateUserOptions options)
        {
            var uri = GetUserManagementUri(options.AuthenticationDomain, username);
            Logger.LogInformation($"Attempting to create user with username {username} - {uri}");

            try
            {
                try
                {
                    // throw UserAlreadyExitsException if user already exists
                    await GetAsync(username, GetUserOptions.Default);
                    throw new UserAlreadyExistsException(username);
                }
                catch (UserNotFoundException)
                {
                    // expected
                }

                // create user
                var content = new FormUrlEncodedContent(GetUserFormValues(password, username, roles));
                var result = await _client.PutAsync(uri, content, options.CancellationToken).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();
            }
            catch (UserAlreadyExistsException)
            {
                Logger.LogError($"Failed to create user with username {username} as it already exists");
                throw;
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Error trying to upsert user - {uri}");
                throw;
            }
        }

        public async Task UpsertAsync(string username, IEnumerable<UserRole> roles, UpsertUserOptions options)
        {
            var uri = GetUserManagementUri(options.AuthenticationDomain, username);
            Logger.LogInformation($"Attempting to create user with username {username} - {uri}");

            try
            {
                var content = new FormUrlEncodedContent(GetUserFormValues(options.Password, username, roles));
                var result = await _client.PutAsync(uri, content, options.CancellationToken).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Error trying to upsert user - {uri}");
                throw;
            }
        }

        public async Task DropAsync(string username, DropUserOptions options)
        {
            var uri = GetUserManagementUri(options.AuthenticationDomain, username);
            Logger.LogInformation($"Attempting to drop user with username {username} - {uri}");


            try
            {
                // check user exists, will throw UserNotFoundException if not
                await GetAsync(username, GetUserOptions.Default);

                // remove user
                var result = await _client.DeleteAsync(uri, options.CancellationToken).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();
            }
            catch (UserNotFoundException)
            {
                Logger.LogError($"Unable to drop user with username {username} as it does not exist");
                throw;
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Error trying to drop user with username {username} - {uri}");
                throw;
            }
        }
    }
}
