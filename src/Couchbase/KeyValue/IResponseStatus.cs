using Couchbase.Core.Compatibility;
using Couchbase.Core.IO.Operations;

namespace Couchbase.KeyValue;

[InterfaceStability(Level.Volatile)]
public interface IResponseStatus
{
    /// <summary>
    /// The <see cref="ResponseStatus"/> returned by the server for each operation.
    /// </summary>
    ResponseStatus Status { get; }
}
