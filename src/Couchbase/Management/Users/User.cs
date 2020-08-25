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
