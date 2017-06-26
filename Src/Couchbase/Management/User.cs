using System.Collections.Generic;
using Newtonsoft.Json;

namespace Couchbase.Management
{
    /// <summary>
    /// Represents a Couchbase user that can perform operations.
    /// Available operations are defined by their assigned list of <see cref="Role"/>s.
    /// </summary>
    public class User
    {
        [JsonProperty("id")]
        public string Username { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("domain")]
        public string Domain { get; set; }

        [JsonProperty("roles")]
        public IEnumerable<Role> Roles { get; set; }
    }
}