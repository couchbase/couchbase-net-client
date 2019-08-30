using System.Collections.Generic;

namespace Couchbase.Management
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
    }
}