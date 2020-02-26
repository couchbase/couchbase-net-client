using System.Collections.Generic;
using System.Net;

namespace Couchbase.Core.Exceptions.Search
{
    /// <remarks>Uncommitted</remarks>
    public class SearchErrorContext : IErrorContext
    {
        public string IndexName{ get; internal set; }

        public string Query { get; internal set; }

        public string Parameters { get; internal set; }

        public HttpStatusCode HttpStatus { get; internal set; }

        public string ClientContextId { get; internal set; }

        public string Statement { get; internal set; }

        public string Message { get; internal set; }

        public string Errors { get; internal set; }
    }
}
