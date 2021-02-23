namespace Couchbase.Core.Diagnostics.Tracing.ThresholdTracing
{
    /// <summary>
    /// The following properties must be collected for each trace (if available) and then logged as specified under JSON Output Format.
    /// </summary>
    public static class ThresholdTags
    {
        /// <summary>
        /// The duration of the outer request span
        /// </summary>
        /// <remarks>In Microseconds as a <see cref="uint"/></remarks>
        public static string TotalDuration = "total_duration";

        /// <summary>
        /// The duration of the encode span, if present
        /// </summary>
        /// <remarks>In Microseconds as a <see cref="uint"/></remarks>
        public static string EncodeDuration = "request_encoding_duration";

        /// <summary>
        /// The duration of the last dispatch span if present
        /// </summary>
        /// <remarks>In Microseconds as a <see cref="uint"/></remarks>
        public static string DispatchDuration = "dispatch_to_server_duration";
    }
}
