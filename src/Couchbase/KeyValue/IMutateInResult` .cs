using Couchbase.Core.Compatibility;
using Couchbase.Core.IO.Serializers;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <inheritdoc cref="IMutateInResult"/>
    /// <typeparam name="TDocument">Type of the document.</typeparam>
    [InterfaceStability(Level.Volatile)]
    public interface IMutateInResult<TDocument> : IMutateInResult, ITypeSerializerProvider
    {
    }
}
