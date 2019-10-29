namespace Couchbase.Core.Exceptions
{
    public class IndexNotFoundException : CouchbaseException
    {
        public IndexNotFoundException(){}

        public IndexNotFoundException(IErrorContext context)
        {
            Context = context;
        }
    }
}
