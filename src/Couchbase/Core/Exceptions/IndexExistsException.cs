namespace Couchbase.Core.Exceptions
{
    public class IndexExistsException : CouchbaseException
    {
        public IndexExistsException(){}

        public IndexExistsException(IErrorContext context)
        {
            Context = context;
        }
    }
}
