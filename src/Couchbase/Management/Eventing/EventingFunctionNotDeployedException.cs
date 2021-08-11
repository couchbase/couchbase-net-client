namespace Couchbase.Management.Eventing
{
    /// <summary>
    /// Thrown if the function is not deployed, but the action expects it to be.
    /// </summary>
    public class EventingFunctionNotDeployedException : CouchbaseException
    {
        public EventingFunctionNotDeployedException(string message) : base(message) { }
    }
}
