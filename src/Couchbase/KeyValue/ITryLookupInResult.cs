using Couchbase.Core.Exceptions.KeyValue;

namespace Couchbase.KeyValue;

/// <summary>
/// Provides an interface for supporting the state of a document if the server
/// returns a PathNotFound status, as opposed to throwing a <see cref="PathNotFoundException"/>
/// like in the regular GetAsync methods.
/// </summary>
public interface ITryLookupInResult : ILookupInResult
{
    /// <summary>
    /// If false, the document does not exist on the server for a given key.
    /// </summary>
    bool DocumentExists { get; }
}
