using System.Collections.Generic;
using System.Net;
using Couchbase.Query;

namespace Couchbase.Core.Exceptions.Analytics
{
    /// <remarks>Uncommitted</remarks>
    public class AnalyticsErrorContext : IErrorContext
    {
        public string Statement { get; internal set; }

        public HttpStatusCode HttpStatus { get; internal set; }

        public string ClientContextId { get; internal set; }

        public string Message { get; internal set; }

        public string Parameters { get; internal set; }

        public List<Error> Errors { get; internal set; }
    }
}
