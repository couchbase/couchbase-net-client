namespace Couchbase.Core.Exceptions.Query
{
    public class PreparedStatementException : CouchbaseException
    {
        public PreparedStatementException(QueryErrorContext context) : base(context.Message)
        {
            Context = context;
        }
    }
}
