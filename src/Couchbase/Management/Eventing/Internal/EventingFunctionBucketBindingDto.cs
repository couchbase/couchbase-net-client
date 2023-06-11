using System.Text.Json.Serialization;

namespace Couchbase.Management.Eventing.Internal
{
    // Helper used so we can keep properties internal on the publicly exposed EventingFunctionBucketBinding.
    // Internal properties can't be serialized by JsonSerializerContext.
    internal class EventingFunctionBucketBindingDto
    {
        [JsonPropertyName("alias")]
        public string Alias { get; set; }

        [JsonPropertyName("bucket_name")]
        public string BucketName { get; set; }

        [JsonPropertyName("scope_name")]
        public string ScopeName { get; set; }

        [JsonPropertyName("collection_name")]
        public string CollectionName { get; set; }

        [JsonConverter(typeof(BucketAccessConverter))]
        [JsonPropertyName("access")]
        public EventingFunctionBucketAccess Access { get; set; }

        public static explicit operator EventingFunctionBucketBindingDto(EventingFunctionBucketBinding func) =>
            new()
            {
                Alias = func.Alias,
                BucketName = func.BucketName,
                ScopeName = func.ScopeName,
                CollectionName = func.CollectionName,
                Access = func.Access
            };

        public static explicit operator EventingFunctionBucketBinding(EventingFunctionBucketBindingDto func) =>
            new()
            {
                Alias = func.Alias,
                BucketName = func.BucketName,
                ScopeName = func.ScopeName,
                CollectionName = func.CollectionName,
                Access = func.Access
            };
    }
}
