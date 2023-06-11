using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using Couchbase.Query;

namespace Couchbase.Management.Eventing.Internal
{
    internal class EventingFunctionSettingsResponseDto
    {
        [JsonPropertyName("cpp_worker_thread_count")]
        public int CppWorkerThreadCount { get; set; } = 2;

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

        [JsonPropertyName("lcb_inst_capacity")]
        public int LcbInstCapacity { get; set; } = 10;

        [JsonPropertyName("lcb_retry_count")]
        private int LcbRetryCount { get; set; } = 0;

        [JsonPropertyName("lcb_timeout")]
        [JsonConverter(typeof(SecondsTimeSpanConverter))]
        public TimeSpan LcbTimeout { get; set; } = TimeSpan.FromSeconds(5);

        [JsonConverter(typeof(QueryScanConsistencyConverter))]
        [JsonPropertyName("n1ql_consistency")]
        public QueryScanConsistency QueryConsistency { get; set; } = QueryScanConsistency.NotBounded;

        [JsonPropertyName("num_timer_partitions")]
        public int NumTimerPartitions { get; set; }

        [JsonPropertyName("sock_batch_size")]
        public int SockBatchSize { get; set; } = 1;

        public int TickDuration { get; set; } = 60000;

        [JsonPropertyName("timer_context_size")]
        public int TimerContextSize { get; set; } = 1024;

        [JsonPropertyName("user_prefix")]
        public string UserPrefix { get; set; } = "eventing";

        [JsonPropertyName("bucket_cache_size")]
        public int BucketCacheSize { get; set; } = 67108864;

        [JsonPropertyName("bucket_cache_age")]
        public int BucketCacheAge { get; set; } = 1000;

        [JsonPropertyName("curl_max_allowed_resp_size")]
        public int CurlMaxAllowedRespSize { get; set; } = 100;

        [JsonPropertyName("n1ql_prepare_all")]
        public bool QueryPrepareAll { get; set; }

        [JsonPropertyName("worker_count")]
        public int WorkerCount { get; set; } = 1;

        [JsonPropertyName("handler_headers")]
        public List<string> HandlerHeaders { get; set; } = new();

        [JsonPropertyName("handler_footers")]
        public List<string> HandlerFooters { get; set; } = new();

        [JsonPropertyName("enable_applog_rotation")]
        public bool EnableAppLogRotation { get; set; } = true;

        [JsonPropertyName("app_log_dir")]
        public string AppLogDir { get; set; }

        [JsonPropertyName("app_log_max_size")]
        public int AppLogMaxSize { get; set; } = 41943040;

        [JsonPropertyName("app_log_max_files")]
        public int AppLogMaxFiles { get; set; } = 10;

        [JsonPropertyName("checkpoint_interval")]
        public int CheckpointInterval { get; set; } = 1;

        public static explicit operator EventingFunctionSettings(EventingFunctionSettingsResponseDto settings) =>
            new()
            {
                CppWorkerThreadCount = settings.CppWorkerThreadCount,
                DcpStreamBoundary = settings.DcpStreamBoundary,
                Description = settings.Description,
                DeploymentStatus = settings.DeploymentStatus,
                ProcessingStatus = settings.ProcessingStatus,
                LanguageCompatibility = settings.LanguageCompatibility,
                LogLevel = settings.LogLevel,
                ExecutionTimeout = settings.ExecutionTimeout,
                LcbInstCapacity = settings.LcbInstCapacity,
                LcbTimeout = settings.LcbTimeout,
                QueryConsistency = settings.QueryConsistency,
                NumTimerPartitions = settings.NumTimerPartitions,
                SockBatchSize = settings.SockBatchSize,
                TickDuration = settings.TickDuration,
                TimerContextSize = settings.TimerContextSize,
                UserPrefix = settings.UserPrefix,
                BucketCacheSize = settings.BucketCacheSize,
                BucketCacheAge = settings.BucketCacheAge,
                CurlMaxAllowedRespSize = settings.CurlMaxAllowedRespSize,
                QueryPrepareAll = settings.QueryPrepareAll,
                WorkerCount = settings.WorkerCount,
                HandlerHeaders = settings.HandlerHeaders,
                HandlerFooters = settings.HandlerFooters,
                EnableAppLogRotation = settings.EnableAppLogRotation,
                AppLogDir = settings.AppLogDir,
                AppLogMaxSize = settings.AppLogMaxSize,
                AppLogMaxFiles = settings.AppLogMaxFiles,
                CheckpointInterval = settings.CheckpointInterval
            };
    }
}
