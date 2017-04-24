using System.Collections.Generic;

namespace Couchbase.Management
{
    /// <summary>
    /// Represents a Couchbase user that can perform operations.
    /// Available operations are defined by their assigned list of <see cref="Role"/>s.
    /// </summary>
    public class User
    {
        public string Username { get; set; }

        public string Name { get; set; }

        public string Domain { get; set; }

        public IEnumerable<Role> Roles { get; set; }

        internal struct UserData
        {
            public string id { get; set; }
            public string name { get; set; }
            public string domain { get; set; }
            public IEnumerable<Role> roles { get; set; }

            public User ToUser()
            {
                return new User
                {
                    Username = id,
                    Name = name,
                    Domain = domain,
                    Roles = roles
                };
            }
        }
    }
}