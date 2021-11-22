using System;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// Extends <see cref="ITypeSerializer"/> with methods for parsing projections from key/value responses.
    /// If not present, calling <see cref="IGetResult.ContentAs{T}"/> for a LookupIn sub-document operation
    /// or for a get operation with projections will fallback to using <see cref="DefaultSerializer"/>.
    /// </summary>
    public interface IProjectableTypeDeserializer : ITypeSerializer
    {
        /// <summary>
        /// Create a new <see cref="IProjectionBuilder"/> based on this serializer.
        /// </summary>
        /// <param name="logger">Logger for logging warnings or errors.</param>
        /// <returns>A new <see cref="IProjectionBuilder"/>.</returns>
        IProjectionBuilder CreateProjectionBuilder(ILogger logger);
    }
}
