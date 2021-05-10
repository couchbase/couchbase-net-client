using Couchbase.Utils;
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
            return new()
            {
                Description = json.GetTokenValue<string>("desc"),
                DisplayName = json.GetTokenValue<string>("name"),
                Role = json.GetTokenValue<string>("role")
            };
        }
    }
}
