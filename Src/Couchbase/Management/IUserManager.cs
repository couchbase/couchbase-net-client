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

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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

#endregion
