using Couchbase.Core.Exceptions.Query;

namespace Couchbase.Core.Exceptions
{
    public class PlanningFailureException : CouchbaseException
    {
        public PlanningFailureException(QueryErrorContext context) : base(context.Message)
        {
            Context = context;
        }
    }
}
