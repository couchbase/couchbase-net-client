using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Couchbase.Management.Users
{
    public class Group
    {
        public string Name { get; }
        public string Description { get; internal set; }
        public IEnumerable<Role> Roles { internal get; set; }
        public string LdapGroupReference { get; internal set; }

        public Group(string name)
        {
            Name = name;
        }

        internal static Group FromJson(JToken json)
        {
            return new Group(json["id"].Value<string>())
            {
                Description = json["description"].Value<string>(),
                LdapGroupReference = json["ldap_group_ref"].Value<string>(),
                Roles = json["roles"].Select(role =>
                    new Role(role["role"].Value<string>(),
                        role["bucket_name"]?.Value<string>(),
                        role["scope_name"]?.Value<string>(),
                        role["collection_name"]?.Value<string>()))
            };
        }
    }
}
