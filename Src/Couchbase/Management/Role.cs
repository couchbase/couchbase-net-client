using Newtonsoft.Json;

namespace Couchbase.Management
{
    /// <summary>
    /// Represents a Couchbase role that allows a cluster operation such as KV Read / Write, Select N1ql, etc.
    /// Roles can also be bucket specific.
    /// </summary>
    public class Role
    {
        [JsonProperty("role")]
        public string Name { get; set; }

        [JsonProperty("bucket_name")]
        public string BucketName { get; set; }
    }
}