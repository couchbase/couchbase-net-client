using Couchbase.Core.Compatibility;
using Couchbase.Core.IO.Serializers;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <inheritdoc cref="ILookupInResult"/>
    /// <typeparam name="TDocument">Type of the document.</typeparam>
    [InterfaceStability(Level.Volatile)]
    public interface ILookupInResult<TDocument> : ILookupInResult, ITypeSerializerProvider
    {
    }
}
