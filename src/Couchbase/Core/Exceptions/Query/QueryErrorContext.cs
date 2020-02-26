using System.Collections.Generic;
using System.Net;
using Couchbase.Query;

namespace Couchbase.Core.Exceptions.Query
{
    /// <remarks>Uncommitted</remarks>
    public class QueryErrorContext : IErrorContext
    {
        public string Statement { get; internal set; }

        public string ClientContextId { get; internal set; }

        public string Parameters { get; internal set; }

        public HttpStatusCode HttpStatus { get; internal set; }

        public QueryStatus QueryStatus { get; internal set; }

        public List<Error> Errors { get; internal set; }

        public string Message { get; internal set; }
    }
}
