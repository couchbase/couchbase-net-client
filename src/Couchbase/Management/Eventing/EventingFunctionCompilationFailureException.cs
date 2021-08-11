namespace Couchbase.Management.Eventing
{
    /// <summary>
    /// Thrown when the compilation of the function code failed.
    /// </summary>
    public class EventingFunctionCompilationFailureException : CouchbaseException
    {
        public EventingFunctionCompilationFailureException(string message) : base(message) { }
    }
}
