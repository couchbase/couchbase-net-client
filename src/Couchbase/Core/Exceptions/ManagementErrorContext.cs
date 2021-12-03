using System.Net;

#nullable enable

namespace Couchbase.Core.Exceptions
{
    public class ManagementErrorContext : IErrorContext
    {
        public string? Message { get; init; }
        public string? Statement { get; init; }
        public string? ClientContextId { get; init; }
        public HttpStatusCode HttpStatus { get; init; }
    }
}
