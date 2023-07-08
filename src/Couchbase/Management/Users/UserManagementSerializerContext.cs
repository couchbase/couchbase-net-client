using System.Collections.Generic;
using System.Text.Json.Serialization;

#nullable enable

namespace Couchbase.Management.Users
{
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(UserAndMetadataDto))]
    [JsonSerializable(typeof(List<UserAndMetadataDto>))]
    [JsonSerializable(typeof(GroupDto))]
    [JsonSerializable(typeof(List<GroupDto>))]
    [JsonSerializable(typeof(List<RoleAndDescription>))]
    internal partial class UserManagementSerializerContext : JsonSerializerContext
    {
    }
}
