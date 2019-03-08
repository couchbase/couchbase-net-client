using System;
using System.Collections.Generic;

namespace Couchbase.Services.Query
{
    /// <summary>
    /// A request error happens when there is a problem with the REST request itself, e.g. missing a required parameter.
    /// </summary>
    public class QueryErrorException : QueryException
    {
        public QueryErrorException(string message, QueryStatus status, IList<Error> errors)
            : base(message, status, errors)
        {
        }

        public QueryErrorException(string message, QueryStatus status, IList<Error> errors, Exception innerException)
            : base(message, status, errors, innerException)
        {
        }
    }
}
