using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Management.Users
{
    public interface IUserManager
    {
        Task<UserAndMetaData> GetUserAsync(string username, GetUserOptions? options = null);

        Task<IEnumerable<UserAndMetaData>> GetAllUsersAsync(GetAllUsersOptions? options = null);

        Task UpsertUserAsync(User user, UpsertUserOptions? options = null);

        Task DropUserAsync(string username, DropUserOptions? options = null);

        Task ChangeUserPasswordAsync(string password, ChangePasswordOptions? options = null);

        Task<IEnumerable<RoleAndDescription>> GetRolesAsync(AvailableRolesOptions? options = null);

        Task<Group> GetGroupAsync(string groupName, GetGroupOptions? options = null);

        Task<IEnumerable<Group>> GetAllGroupsAsync(GetAllGroupsOptions? options = null);

        Task UpsertGroupAsync(Group group, UpsertGroupOptions? options = null);

        Task DropGroupAsync(string groupName, DropGroupOptions? options = null);
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
