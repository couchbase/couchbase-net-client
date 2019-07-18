using System.Collections.Generic;
using Newtonsoft.Json;

namespace Couchbase.Management
{
    public class User
    {
        [JsonProperty("id")]
        public string Username { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("domain")]
        public string Domain { get; set; }

        [JsonProperty("roles")]
        public IEnumerable<UserRole> Roles { get; set; }

        [JsonProperty("groups")]
        public IEnumerable<string> Groups { get; set; }
    }
}
