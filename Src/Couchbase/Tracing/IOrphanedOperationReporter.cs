namespace Couchbase.Tracing
{
    /// <summary>
    /// Collections and reports orphaned server responses.
    /// Typically this is because the operation timed out before the response
    /// was received.
    /// </summary>
    public interface IOrphanedOperationReporter
    {
        /// <summary>
        /// Adds the specified operation.
        /// </summary>
        /// <param name="endpoint">The hostname (IP) and port where the response was dispatched to.</param>
        /// <param name="operationId">The operation correlation ID.</param>
        /// <param name="serverDuration">Server duration of the operation.</param>
        void Add(string endpoint, string operationId, long? serverDuration);
    }
}
