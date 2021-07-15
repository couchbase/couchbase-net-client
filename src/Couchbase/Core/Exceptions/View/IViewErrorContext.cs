using System.Net;
using Couchbase.Core.Compatibility;

#nullable enable

namespace Couchbase.Core.Exceptions.View
{
    [InterfaceStability(Level.Uncommitted)]
    public interface IViewErrorContext : IErrorContext
    {
        string? DesignDocumentName { get; }
        string? Parameters { get; }
        string? ViewName { get; }
        HttpStatusCode HttpStatus { get; }
        string? ClientContextId { get; }
        string? Errors { get; }
    }
}
