using System.Collections.Generic;
using System.Net;
using Couchbase.Core.Compatibility;
using Couchbase.Query;

#nullable enable

namespace Couchbase.Core.Exceptions.Query
{
    [InterfaceStability(Level.Uncommitted)]
    public interface IQueryErrorContext : IErrorContext
    {
        string? Statement { get; }
        string? ClientContextId { get; }
        string? Parameters { get; }
        HttpStatusCode HttpStatus { get; }
        QueryStatus QueryStatus { get; }
        List<Error>? Errors { get; }
    }
}
