using System.Net;

namespace Couchbase.Core.Exceptions.Analytics
{
    /// <remarks>Uncommitted</remarks>
    public class AnalyticsErrorContext : IErrorContext
    {
        public string Statement { get; internal set; }

        public HttpStatusCode HttpStatus { get; internal set; }

        public string ClientContextId { get; internal set; }

        public string Message { get; internal set; }
    }
}
