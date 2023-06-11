using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable

namespace Couchbase.Management.Eventing.Internal
{
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(ErrorResponse))]
    [JsonSerializable(typeof(EventingStatus))]
    [JsonSerializable(typeof(EventingFunction))]
    [JsonSerializable(typeof(List<EventingFunction>), TypeInfoPropertyName = "EventingFunctionList")]
    [JsonSerializable(typeof(EventingFunctionRequestDto))]
    [JsonSerializable(typeof(EventingFunctionResponseDto))]
    [JsonSerializable(typeof(EventingFunctionSettingsRequestDto))]
    [JsonSerializable(typeof(EventingFunctionSettingsResponseDto))]
    [JsonSerializable(typeof(EventingFunctionBucketBindingDto))]
    [JsonSerializable(typeof(EventingFunctionUrlNoAuth))]
    [JsonSerializable(typeof(EventingFunctionUrlAuthBasic))]
    [JsonSerializable(typeof(EventingFunctionUrlAuthDigest))]
    [JsonSerializable(typeof(EventingFunctionUrlAuthBearer))]
    internal partial class EventingSerializerContext : JsonSerializerContext
    {
        private static EventingSerializerContext? _primary;

        /// <summary>
        /// An alternate version that ignores comments. This is particularly important for unit tests,
        /// which have comments in the JSON files.
        /// </summary>
        public static EventingSerializerContext Primary => _primary ??= new EventingSerializerContext(
            new JsonSerializerOptions(s_defaultOptions)
            {
                ReadCommentHandling = JsonCommentHandling.Skip
            });
    }
}
