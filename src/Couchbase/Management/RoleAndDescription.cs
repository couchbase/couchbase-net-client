using Newtonsoft.Json.Linq;

namespace Couchbase.Management
{
    public class RoleAndDescription
    {
        public Role Role { get; internal set; }
        public string DisplayName { get; internal set; }
        public string Description { get; internal set; }

        internal static RoleAndDescription FromJson(JToken json)
        {
            return new RoleAndDescription
            {
                Description = json["desc"].Value<string>(),
                DisplayName = json["name"].Value<string>(),
                Role = new Role
                {
                    Name = json["role"].Value<string>(),
                    Bucket = json["bucket_name"]?.Value<string>()
                }
            };
        }
    }
}