using Couchbase.Core.Exceptions.Query;

namespace Couchbase.Core.Exceptions
{
    public class IndexFailureException : CouchbaseException
    {
        public IndexFailureException(QueryErrorContext context) : base(context.Message)
        {
            Context = context;
        }
    }
}
