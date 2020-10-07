namespace Couchbase.Core.Exceptions
{
    public class FeatureNotAvailableException : CouchbaseException
    {
        public FeatureNotAvailableException()
            : base("Feature Not Available")
        {
        }

        public FeatureNotAvailableException(string msg)
            : base(msg)
        {
        }
    }
}
