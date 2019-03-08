using System;
using System.Collections.Generic;

namespace Couchbase.Services.Query
{
    /// <summary>
    /// A service error means there is a problem that prevents the request being fulfilled.
    /// </summary>
    public class QueryServiceException : QueryException
    {
        public QueryServiceException(string message, QueryStatus status, IList<Error> errors)
            : base(message, status, errors)
        {
        }

        public QueryServiceException(string message, QueryStatus status, IList<Error> errors, Exception innerException)
            : base(message, status, errors, innerException)
        {
        }
    }
}
