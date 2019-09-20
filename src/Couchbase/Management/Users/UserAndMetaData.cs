using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

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

        internal static UserAndMetaData FromJson(JToken json)
        {
            var roles = new List<Role>();
            var rolesAndOrigins = new List<RoleAndOrigins>();

            if (json["roles"] != null)
            {
                foreach (var row in json["roles"])
                {
                    var role = new Role
                    {
                        Name = row["role"].Value<string>(),
                        Bucket = row["bucket_name"]?.Value<string>()
                    };

                    roles.Add(role);
                    rolesAndOrigins.Add(new RoleAndOrigins
                    {
                        Role = role,
                        Origins = row["origins"].Select(origin => new Origin
                        {
                            Name = origin["name"]?.Value<string>(),
                            Type = Extensions.Value<string>(origin["type"]),
                        })
                    });
                }
            }

            return new UserAndMetaData(json["id"].Value<string>())
            {
                DisplayName = json["name"].Value<string>(),
                Domain = json["domain"].Value<string>(),
                Groups = json["groups"].Values<string>(),
                PasswordChanged = DateTimeOffset.Parse(json["password_change_date"].Value<string>()),
                ExternalGroups = json["external_groups"].Values<string>(),
                EffectiveRoles = roles,
                EffectiveRolesAndOrigins = rolesAndOrigins
            };
        }
    }
}
