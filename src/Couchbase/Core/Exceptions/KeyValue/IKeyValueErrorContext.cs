using Couchbase.Core.Compatibility;
using Couchbase.Core.IO.Operations;

#nullable enable

namespace Couchbase.Core.Exceptions.KeyValue
{
    /// <remarks>Uncommitted</remarks>
    [InterfaceStability(Level.Uncommitted)]
    public interface IKeyValueErrorContext : IErrorContext
    {
        string? DispatchedFrom { get; }
        string? DispatchedTo { get; }
        string? DocumentKey { get; }
        string? ClientContextId { get; }
        ulong Cas { get; }
        ResponseStatus Status { get; }
        string? BucketName { get; }
        string? CollectionName { get; }
        string? ScopeName { get; }
        OpCode OpCode { get; set; }
    }
}
