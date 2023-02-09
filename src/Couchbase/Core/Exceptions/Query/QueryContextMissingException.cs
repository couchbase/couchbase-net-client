using Couchbase.Core.Compatibility;

namespace Couchbase.Core.Exceptions.Query;

/// <summary>The server version requires that the query_context must be included in the query request.
/// </summary>
/// <remarks>Uncommitted</remarks>
[InterfaceStability(Level.Uncommitted)]
public class QueryContextMissingException : QueryException
{
    public QueryContextMissingException(QueryErrorContext context) : base(context.Message ?? "query_context required error.")
    {
        Context = context;
    }
}
