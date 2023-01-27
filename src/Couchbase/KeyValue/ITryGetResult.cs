using Couchbase.Core.Exceptions.KeyValue;

namespace Couchbase.KeyValue;

/// <summary>
/// Provides an interface for supporting the state of a document if the server
/// returns a KeyNotFound status, as opposed to throwing a <see cref="DocumentNotFoundException"/>
/// like in the regular GetAsync methods.
/// </summary>
public interface ITryGetResult : IGetResult
{
    /// <summary>
    /// If false, the document does not exist on the server for a given key.
    /// </summary>
    bool Exists { get; }
}
