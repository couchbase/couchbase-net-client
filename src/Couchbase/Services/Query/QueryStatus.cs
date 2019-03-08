namespace Couchbase.Services.Query
{
    public enum QueryStatus
    {
        Success,

        Running,

        Errors,

        Completed,

        Stopped,

        Timeout,

        Fatal
    }
}