using System.Collections.Generic;
using System.Text.Json.Serialization;

#nullable enable

namespace Couchbase.Management.Users
{
    internal class GroupDto
    {
        public string Id { get; set; } = "";

        public string? Description { get; set; }

        public List<RoleDto>? Roles { get; set; }

        [JsonPropertyName("ldap_group_ref")]
        public string? LdapGroupReference { get; set; }
    }
}
