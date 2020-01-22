using System.Collections.Generic;
using System.Net;

namespace Couchbase.Core.Exceptions.View
{
    /// <remarks>Uncommitted</remarks>
    public class ViewContextError : IErrorContext
    {
        public string DesignDocumentName { get; internal set; }

        public Dictionary<string, string> Parameters { get; internal set; }

        public string ViewName { get; internal set; }

        public HttpStatusCode HttpStatus { get; internal set; }

        public string ClientContextId { get; internal set; }

        public string Message { get; internal set; }
    }
}
