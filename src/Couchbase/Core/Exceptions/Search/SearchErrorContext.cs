using System.Collections.Generic;
using System.Net;

namespace Couchbase.Core.Exceptions.Search
{
    public class SearchErrorContext : IErrorContext
    {
        public string DispatchedFrom { get; internal set; }

        public string DispatchedTo { get; internal set; }

        public string ContextId { get; internal set; }

        public string IndexName{ get; internal set; }

        public string Query { get; internal set; }

        public Dictionary<string, string> Parameters { get; internal set; }

        public HttpStatusCode HttpStatus { get; internal set; }

        public string Message { get; internal set; }
    }
}
