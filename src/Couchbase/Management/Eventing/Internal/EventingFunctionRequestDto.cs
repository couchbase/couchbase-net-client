using System.Text.Json.Serialization;

namespace Couchbase.Management.Eventing.Internal
{
    // Helper used so we can keep DeploymentConfig internal on the publicly exposed EventingFunction.
    // Internal properties can't be serialized by JsonSerializerContext.
    internal class EventingFunctionRequestDto
    {
        [JsonPropertyName("appname")]
        [JsonPropertyOrder(3)]
        public string Name { get; set; }

        [JsonPropertyName("appcode")]
        [JsonPropertyOrder(4)]
        public string Code { get; set; }

        [JsonPropertyName("version")]
        [JsonPropertyOrder(2)]
        public string Version { get; set; } = "external";

        [JsonPropertyOrder(1)]
        public EventingFunctionSettings Settings { get; set; }

        [JsonPropertyName("depcfg")]
        [JsonPropertyOrder(0)]
        public DeploymentConfig DeploymentConfig { get; set; }

        public static explicit operator EventingFunctionRequestDto(EventingFunction func) =>
            new()
            {
                Name = func.Name,
                Code = func.Code,
                Version = func.Version,
                Settings = func.Settings,
                DeploymentConfig = func.DeploymentConfig,
            };
    }
}
