namespace Couchbase.Management.Eventing
{
    /// <summary>
    /// Thrown when source and metadata key spaces are the same.
    /// </summary>
    // ReSharper disable once IdentifierTypo
    public class EventingFunctionIdenticalKeyspaceException : CouchbaseException
    {
        public EventingFunctionIdenticalKeyspaceException(string message) : base(message) { }
    }
}
