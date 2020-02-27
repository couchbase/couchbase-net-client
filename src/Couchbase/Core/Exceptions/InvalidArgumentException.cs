namespace Couchbase.Core.Exceptions
{
    public class InvalidArgumentException : CouchbaseException
    {
        public InvalidArgumentException(string message) : base(message)
        {
        }
        public InvalidArgumentException() {}
    }
}
