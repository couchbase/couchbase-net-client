using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using Couchbase.Query;

namespace Couchbase.Management.Eventing.Internal
{
    internal class EventingFunctionSettingsRequestDto
    {
        [JsonPropertyName("dcp_stream_boundary")]
        [JsonConverter(typeof(EventingFunctionDcpBoundaryConverter))]
        public EventingFunctionDcpBoundary DcpStreamBoundary { get; set; } = EventingFunctionDcpBoundary.Everything;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("deployment_status")]
        [JsonConverter(typeof(EventingFunctionDeploymentStatusConverter))]
        public EventingFunctionDeploymentStatus DeploymentStatus { get; set; } =
            EventingFunctionDeploymentStatus.Undeployed;

        [JsonPropertyName("processing_status")]
        [JsonConverter(typeof(EventingFunctionProcessingStatusConverter))]
        public EventingFunctionProcessingStatus ProcessingStatus { get; set; } =
            EventingFunctionProcessingStatus.Paused;

        [JsonPropertyName("language_compatibility")]
        [JsonConverter(typeof(CompatibilityConverter))]
        public EventingFunctionLanguageCompatibility LanguageCompatibility { get; set; } = EventingFunctionLanguageCompatibility.Version_6_6_2;

        [JsonPropertyName("log_level")]
        [JsonConverter(typeof(LogLevelConverter))]
        public EventingFunctionLogLevel LogLevel { get; set; } = EventingFunctionLogLevel.Info;

        [JsonPropertyName("execution_timeout")]
        [JsonConverter(typeof(SecondsTimeSpanConverter))]
        public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromSeconds(60);

        [JsonConverter(typeof(QueryScanConsistencyConverter))]
        [JsonPropertyName("n1ql_consistency")]
        public QueryScanConsistency QueryConsistency { get; set; } = QueryScanConsistency.NotBounded;

        [JsonPropertyName("timer_context_size")]
        public int TimerContextSize { get; set; } = 1024;

        [JsonPropertyName("user_prefix")]
        public string UserPrefix { get; set; } = "eventing";

        [JsonPropertyName("worker_count")]
        public int WorkerCount { get; set; } = 1;

        public static explicit operator EventingFunctionSettingsRequestDto(EventingFunctionSettings settings) =>
            new()
            {
                DcpStreamBoundary = settings.DcpStreamBoundary,
                Description = settings.Description,
                DeploymentStatus = settings.DeploymentStatus,
                ProcessingStatus = settings.ProcessingStatus,
                LanguageCompatibility = settings.LanguageCompatibility,
                LogLevel = settings.LogLevel,
                ExecutionTimeout = settings.ExecutionTimeout,
                QueryConsistency = settings.QueryConsistency,
                TimerContextSize = settings.TimerContextSize,
                UserPrefix = settings.UserPrefix,
                WorkerCount = settings.WorkerCount,
            };
    }
}
