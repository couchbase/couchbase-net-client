using System.Net;
using Couchbase.Core.Compatibility;

#nullable enable

namespace Couchbase.Core.Exceptions.Search
{
    [InterfaceStability(Level.Uncommitted)]
    public interface ISearchErrorContext : IErrorContext
    {
        string? IndexName { get; }
        string? Query { get; }
        string? Parameters { get; }
        HttpStatusCode HttpStatus { get; }
        string? ClientContextId { get; }
        string? Statement { get; }
        string? Errors { get; }
    }
}
