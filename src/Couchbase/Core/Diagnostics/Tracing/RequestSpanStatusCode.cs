namespace Couchbase.Core.Diagnostics.Tracing
{
    /// <summary>
    /// Status code for a <see cref="IRequestSpan"/>, following OpenTelemetry conventions.
    /// </summary>
    public enum RequestSpanStatusCode
    {
        /// <summary>
        /// The default status, indicating status has not been explicitly set.
        /// </summary>
        Unset = 0,

        /// <summary>
        /// The operation completed successfully.
        /// </summary>
        Ok = 1,

        /// <summary>
        /// The operation failed with an error.
        /// </summary>
        Error = 2
    }
}
