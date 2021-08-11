namespace Couchbase.Management.Eventing
{
    /// <summary>
    /// Thrown when a function is deployed but not “fully” bootstrapped.
    /// </summary>
    public class EventingFunctionNotBootstrappedException : CouchbaseException
    {
        public EventingFunctionNotBootstrappedException(string message) : base(message) { }
    }
}
