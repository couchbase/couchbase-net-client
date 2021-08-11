namespace Couchbase.Management.Eventing
{
    /// <summary>
    /// Thrown if the function is not found.
    /// </summary>
    public class EventingFunctionNotFoundException : CouchbaseException
    {
        public EventingFunctionNotFoundException(string message) : base(message) { }
    }
}
