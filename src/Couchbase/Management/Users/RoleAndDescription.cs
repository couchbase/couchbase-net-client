using Newtonsoft.Json.Linq;

namespace Couchbase.Management.Users
{
    public class RoleAndDescription
    {
        public string Role { get; internal set; }
        public string DisplayName { get; internal set; }
        public string Description { get; internal set; }

        internal static RoleAndDescription FromJson(JToken json)
        {
            return new RoleAndDescription
            {
                Description = json["desc"].Value<string>(),
                DisplayName = json["name"].Value<string>(),
                Role = json["role"].Value<string>()
            };
        }
    }
}
