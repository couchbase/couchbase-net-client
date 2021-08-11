namespace Couchbase.Management.Eventing
{
    /// <summary>
    /// Thrown when a function is deployed but the action does not expect it to be.
    /// </summary>
    // ReSharper disable once IdentifierTypo
    public class EventingFunctionDeployedException : CouchbaseException
    {
        public EventingFunctionDeployedException(string message) : base(message) { }
    }
}
