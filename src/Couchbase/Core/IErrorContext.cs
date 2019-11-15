namespace Couchbase.Core
{
    public interface IErrorContext
    {
        string DispatchedFrom { get; }
        string DispatchedTo { get; }
        string ContextId { get; }
        string Message { get; }
    }
}
