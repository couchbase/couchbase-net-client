using System;
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Management.Users
{
    public class UserAndMetaData
    {
        public string Username { get; }
        public string DisplayName { get; internal set; }
        public string Domain { get; internal set; }
        public IEnumerable<string> Groups { get; internal set; }
        public IEnumerable<Role> EffectiveRoles { get; internal set; }
        public IEnumerable<RoleAndOrigins> EffectiveRolesAndOrigins { get; internal set; }
        public DateTimeOffset PasswordChanged { get; internal set; }
        public IEnumerable<string> ExternalGroups { get; internal set; }

        public UserAndMetaData(string username)
        {
            Username = username;
        }

        public User User()
        {
            return new User(Username)
            {
                DisplayName = DisplayName,
                Domain = Domain,
                Roles = EffectiveRoles,
                Groups = Groups
            };
        }

        internal static UserAndMetaData FromJson(UserAndMetadataDto userDto)
        {
            var roles = new List<Role>();
            var rolesAndOrigins = new List<RoleAndOrigins>();

            if (userDto.Roles is not null)
            {
                foreach (var row in userDto.Roles)
                {
                    var role = Role.FromJson(row);
                    roles.Add(role);

                    rolesAndOrigins.Add(new RoleAndOrigins
                    {
                        Role = role,
                        Origins = row.Origins?.ToList()
                    });
                }
            }

            return new UserAndMetaData(userDto.Id)
            {
                DisplayName = userDto.Name,
                Domain = userDto.Domain,
                Groups = userDto.Groups,
                PasswordChanged = userDto.PasswordChangeDate.ToUniversalTime(),
                ExternalGroups = userDto.ExternalGroups,
                EffectiveRoles = roles,
                EffectiveRolesAndOrigins = rolesAndOrigins
            };
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
