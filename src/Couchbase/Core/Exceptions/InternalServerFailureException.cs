namespace Couchbase.Core.Exceptions
{
    public class InternalServerFailureException : CouchbaseException
    {
        public InternalServerFailureException()
        {
        }

        public InternalServerFailureException(IErrorContext context)
        {
            Context = context;
        }
    }
}
