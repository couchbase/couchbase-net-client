using Couchbase.Core.IO.Serializers;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Strongly typed version of <see cref="LookupInSpecBuilder"/>.
    /// </summary>
    /// <typeparam name="TDocument">Type of the whole document.</typeparam>
    public class LookupInSpecBuilder<TDocument> : LookupInSpecBuilder, ITypeSerializerProvider
    {
        /// <inheritdoc />
        public ITypeSerializer Serializer { get; }

        /// <summary>
        /// Creates a new LookupInSpecBuilder.
        /// </summary>
        /// <param name="serializer">Type serializer used for generating paths from lambda expressions.</param>
        public LookupInSpecBuilder(ITypeSerializer serializer)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (serializer == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(serializer));
            }

            Serializer = serializer;
        }
    }
}
