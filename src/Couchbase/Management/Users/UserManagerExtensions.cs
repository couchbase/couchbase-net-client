using System;
using System.Collections.Generic;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Management.Users
{
    public static class UserManagerExtensions
    {
        public static Task<UserAndMetaData> GetUserAsync(this IUserManager userManager, string username)
        {
            return userManager.GetUserAsync(username, GetUserOptions.Default);
        }

        public static Task<UserAndMetaData> GetUserAsync(this IUserManager userManager, string username, Action<GetUserOptions> configureOptions)
        {
            var options = new GetUserOptions();
            configureOptions(options);

            return userManager.GetUserAsync(username, options);
        }

        public static Task<IEnumerable<UserAndMetaData>> GetAllUsersAsync(this IUserManager userManager)
        {
            return userManager.GetAllUsersAsync(GetAllUsersOptions.Default);
        }

        public static Task<IEnumerable<UserAndMetaData>> GetAllUsersAsync(this IUserManager userManager, Action<GetAllUsersOptions> configureOptions)
        {
            var options = new GetAllUsersOptions();
            configureOptions(options);

            return userManager.GetAllUsersAsync(options);
        }

        public static Task UpsertUsersAsync(this IUserManager userManager, User user)
        {
            return userManager.UpsertUserAsync(user, UpsertUserOptions.Default);
        }

        public static Task UpsertUsersAsync(this IUserManager userManager, User user, Action<UpsertUserOptions> configureOptions)
        {
            var options = new UpsertUserOptions();
            configureOptions(options);

            return userManager.UpsertUserAsync(user, options);
        }

        public static Task DropUserAsync(this IUserManager userManager, string username)
        {
            return userManager.DropUserAsync(username, DropUserOptions.Default);
        }

        public static Task DropUserAsync(this IUserManager userManager, string username, Action<DropUserOptions> configureOptions)
        {
            var options = new DropUserOptions();
            configureOptions(options);

            return userManager.DropUserAsync(username, options);
        }

        public static Task<IEnumerable<RoleAndDescription>> AvailableRolesAsync(this IUserManager userManager)
        {
            return userManager.GetRolesAsync(AvailableRolesOptions.Default);
        }

        public static Task<IEnumerable<RoleAndDescription>> AvailableRolesAsync(this IUserManager userManager, Action<AvailableRolesOptions> configureOptions)
        {
            var options = new AvailableRolesOptions();
            configureOptions(options);

            return userManager.GetRolesAsync(options);
        }

        public static Task<Group> GetGroupAsync(this IUserManager userManager, string groupName)
        {
            return userManager.GetGroupAsync(groupName, GetGroupOptions.Default);
        }

        public static Task<Group> GetGroupAsync(this IUserManager userManager, string groupName, Action<GetGroupOptions> configureOptions)
        {
            var options = new GetGroupOptions();
            configureOptions(options);

            return userManager.GetGroupAsync(groupName, options);
        }

        public static Task<IEnumerable<Group>> GetAllGroupsAsync(this IUserManager userManager)
        {
            return userManager.GetAllGroupsAsync(GetAllGroupsOptions.Default);
        }

        public static Task<IEnumerable<Group>> GetAllGroupsAsync(this IUserManager userManager, Action<GetAllGroupsOptions> configureOptions)
        {
            var options = new GetAllGroupsOptions();
            configureOptions(options);

            return userManager.GetAllGroupsAsync(options);
        }

        public static Task UpsertGroupAsync(this IUserManager userManager, Group group)
        {
            return userManager.UpsertGroupAsync(group, UpsertGroupOptions.Default);
        }

        public static Task UpsertGroupAsync(this IUserManager userManager, Group group, Action<UpsertGroupOptions> configureOptions)
        {
            var options = new UpsertGroupOptions();
            configureOptions(options);

            return userManager.UpsertGroupAsync(group, options);
        }

        public static Task DropGroupAsync(this IUserManager userManager, string groupName)
        {
            return userManager.DropGroupAsync(groupName, DropGroupOptions.Default);
        }

        public static Task DropGroupAsync(this IUserManager userManager, string groupName, Action<DropGroupOptions> configureOptions)
        {
            var options = new DropGroupOptions();
            configureOptions(options);

            return userManager.DropGroupAsync(groupName, options);
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
