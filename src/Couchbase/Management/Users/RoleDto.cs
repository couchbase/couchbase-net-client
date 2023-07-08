using System.Collections.Generic;
using System.Text.Json.Serialization;

#nullable enable

namespace Couchbase.Management.Users
{
    internal class RoleDto
    {
        public string Role { get; set; } = "";

        [JsonPropertyName("bucket_name")]
        public string? BucketName { get; set; }

        [JsonPropertyName("scope_name")]
        public string? ScopeName { get; set; }

        [JsonPropertyName("collection_name")]
        public string? CollectionName { get; set; }

        public List<Origin>? Origins { get; set; }
    }
}
