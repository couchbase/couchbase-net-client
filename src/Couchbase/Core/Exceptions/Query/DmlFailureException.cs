namespace Couchbase.Core.Exceptions.Query
{
    public class DmlFailureException : CouchbaseException
    {
        public DmlFailureException(QueryErrorContext context) : base("The server failed to execute a DML query")
        {
            Context = context;
        }
    }
}
