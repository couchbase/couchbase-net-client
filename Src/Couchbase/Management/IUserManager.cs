using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Management
{
    public interface IUserManager
    {
        /// <summary>
        /// Adds or replaces an existing Couchbase user with the provided <see cref="username"/>, <see cref="password"/>, <see cref="name"/> and <see cref="roles"/>.
        /// </summary>
        /// <param name="domain">The authentication domain.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="name">The full name for the user.</param>
        /// <param name="roles">The list of roles for the user.</param>
        IResult UpsertUser(AuthenticationDomain domain, string username, string password = null, string name = null, params Role[] roles);

        /// <summary>
        /// Asynchronously adds or replaces an existing Couchbase user with the provided <see cref="username"/>, <see cref="password"/>, <see cref="name"/> and <see cref="roles"/>.
        /// </summary>
        /// <param name="domain">The authentication domain.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="name">The full name for the user.</param>
        /// <param name="roles">The roles.</param>
        Task<IResult> UpsertUserAsync(AuthenticationDomain domain, string username, string password = null, string name = null, params Role[] roles);

        /// <summary>
        /// Removes a Couchbase user with the <see cref="username"/>.
        /// </summary>
        /// <param name="domain">The authentication domain.</param>
        /// <param name="username">The username.</param>
        IResult RemoveUser(AuthenticationDomain domain, string username);

        /// <summary>
        /// Asynchronously removes a Couchbase user with the <see cref="username"/>.
        /// </summary>
        /// <param name="domain">The authentication domain.</param>
        /// <param name="username">The username.</param>
        Task<IResult> RemoveUserAsync(AuthenticationDomain domain, string username);

        /// <summary>
        /// Get a list of Couchbase users.
        /// </summary>
        /// <param name="domain">The authentication domain.</param>
        IResult<IEnumerable<User>> GetUsers(AuthenticationDomain domain);

        /// <summary>
        /// Asynchronously Get a list of Couchbase users.
        /// </summary>
        /// <param name="domain">The authentication domain.</param>
        Task<IResult<IEnumerable<User>>> GetUsersAsync(AuthenticationDomain domain);

        /// <summary>
        /// Get a Couchbase user by username.
        /// </summary>
        /// <param name="domain">The authentication domain.</param>
        /// <param name="username">The username.</param>
        IResult<User> GetUser(AuthenticationDomain domain, string username);

        /// <summary>
        /// Asynchronously get a Couchbase user by username.
        /// </summary>
        /// <param name="domain">The authentication domain.</param>
        /// <param name="username">The username.</param>
        Task<IResult<User>> GetUserAsync(AuthenticationDomain domain, string username);
    }
}
