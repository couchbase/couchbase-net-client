namespace Couchbase.Services.Analytics
{
    /// <summary>
    /// Represents the return status of an Analytics query.
    /// </summary>
    public enum AnalyticsStatus
    {
        Success,

        Running,

        Errors,

        Completed,

        Stopped,

        Timeout,

        Fatal,

        Failed
    }
}
