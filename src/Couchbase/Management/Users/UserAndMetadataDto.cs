using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Couchbase.Management.Users
{
    internal class UserAndMetadataDto
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Domain { get; set; }
        public List<string> Groups { get; set; }
        public List<RoleDto> Roles { get; set; }

        [JsonPropertyName("external_groups")]
        public List<string> ExternalGroups { get; set; }

        [JsonPropertyName("password_change_date")]
        public DateTimeOffset PasswordChangeDate { get; set; }
    }
}
