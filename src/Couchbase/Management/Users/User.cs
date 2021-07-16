using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Management.Users
{
    public class User
    {
        public string Username { get; }
        public string DisplayName { get; set; }
        public string Domain { get; set; }
        public IEnumerable<string> Groups { get; set; }
        public IEnumerable<Role> Roles { get; set; }
        public string Password { internal get; set; }

        public User(string username)
        {
            Username = username;
        }

        internal void Validate()
        {
            if (Roles?.Any() ?? false)
            {
                foreach (var role in Roles)
                {
                    role.Validate();
                }
            }
        }

        internal IEnumerable<KeyValuePair<string, string>> GetUserFormValues()
        {
            if (!string.IsNullOrWhiteSpace(DisplayName))
            {
                yield return new KeyValuePair<string, string>("name", DisplayName);
            }

            if (!string.IsNullOrWhiteSpace(Password))
            {
                yield return new KeyValuePair<string, string>("password", Password);
            }

            if (Roles?.Any() ?? false)
            {
                yield return new KeyValuePair<string, string>("roles", string.Join(",", Roles!));
            }

            if (Groups?.Any() ?? false)
            {
                yield return new KeyValuePair<string, string>("groups", string.Join(",", Groups!));
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
