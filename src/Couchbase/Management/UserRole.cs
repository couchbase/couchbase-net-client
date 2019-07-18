using Newtonsoft.Json;

namespace Couchbase.Management
{
    public class UserRole
    {
        [JsonProperty("role")]
        public string Name { get; set; }

        [JsonProperty("bucket_name")]
        public string BucketName { get; set; }
    }
}
