using System.Text.Json.Serialization;

namespace Couchbase.Management.Eventing.Internal
{
    // Helper used so we can keep DeploymentConfig internal on the publicly exposed EventingFunction.
    // Internal properties can't be serialized by JsonSerializerContext.
    internal class EventingFunctionResponseDto
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

        [JsonPropertyName("enforce_schema")]
        public bool EnforceSchema { get; set; }

        [JsonPropertyName("handleruuid")]
        public long HandlerUuid { get; set; }

        [JsonPropertyName("function_instance_id")]
        public string FunctionInstanceId { get; set; }

        [JsonPropertyOrder(1)]
        public EventingFunctionSettings Settings { get; set; } = new();

        [JsonPropertyName("depcfg")]
        [JsonPropertyOrder(0)]
        public DeploymentConfig DeploymentConfig { get; set; } = new();

        public static explicit operator EventingFunction(EventingFunctionResponseDto func) =>
            new()
            {
                Name = func.Name,
                Code = func.Code,
                Version = func.Version,
                EnforceSchema = func.EnforceSchema,
                HandlerUuid = func.HandlerUuid,
                FunctionInstanceId = func.FunctionInstanceId,
                Settings = func.Settings,
                DeploymentConfig = func.DeploymentConfig
            };
    }
}
