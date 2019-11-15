using System.Collections.Generic;
using System.Net;

namespace Couchbase.Core.Exceptions.Query
{
    public class QueryErrorContext : IErrorContext
    {
        public string DispatchedFrom { get; internal set; }

        public string DispatchedTo { get; internal set; }

        public string Statement { get; internal set; }

        public string ContextId { get; internal set; }

        public Dictionary<string, string> Parameters { get; internal set; }

        public HttpStatusCode HttpStatus { get; internal set; }

        public string Message { get; internal set; }
    }
}
