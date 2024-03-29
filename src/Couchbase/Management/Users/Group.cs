using System.Collections.Generic;
using System.Linq;

#nullable enable

namespace Couchbase.Management.Users
{
    public class Group
    {
        public string Name { get; }
        public string? Description { get; internal set; }
        public IEnumerable<Role>? Roles { internal get; set; }
        public string? LdapGroupReference { get; internal set; }

        public Group(string name)
        {
            Name = name;
        }

        internal static Group FromJson(GroupDto dto) =>
            new(dto.Id)
            {
                Description = dto.Description,
                LdapGroupReference = dto.LdapGroupReference,
                Roles = dto.Roles?.Select(Role.FromJson)
            };
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
