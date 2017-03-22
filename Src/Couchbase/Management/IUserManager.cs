using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Management
{
    public interface IUserManager
    {
        /// <summary>
        /// Adds or replaces an existing Couchbase user with the provided <see cref="username"/>, <see cref="password"/>, <see cref="name"/> and <see cref="roles"/>.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="name">The full name for the user.</param>
        /// <param name="roles">The list of roles for the user.</param>
        IResult UpsertUser(string username, string password, string name, params Role[] roles);

        /// <summary>
        /// Asynchronously adds or replaces an existing Couchbase user with the provided <see cref="username"/>, <see cref="password"/>, <see cref="name"/> and <see cref="roles"/>.
        /// </summary>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="name">The full name for the user.</param>
        /// <param name="roles">The roles.</param>
        Task<IResult> UpsertUserAsync(string username, string password, string name, params Role[] roles);

        /// <summary>
        /// Removes a Couchbase user with the <see cref="username"/>.
        /// </summary>
        /// <param name="username">The username.</param>
        IResult RemoveUser(string username);

        /// <summary>
        /// Asynchronously removes a Couchbase user with the <see cref="username"/>.
        /// </summary>
        /// <param name="username">The username.</param>
        Task<IResult> RemoveUserAsync(string username);

        /// <summary>
        /// Get a list of Couchbase users.
        /// </summary>
        IResult<IEnumerable<User>> GetUsers();

        /// <summary>
        /// Asynchronously Get a list of Couchbase users.
        /// </summary>
        Task<IResult<IEnumerable<User>>> GetUsersAsync();
    }
}
