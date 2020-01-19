using System;
using System.IO;

#nullable enable

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// Extends <see cref="ITypeSerializer"/> with methods for deserializing
    /// a stream gradually. This allows deserialization to begin before the
    /// entire stream is available, and can reduce memory utilization if the
    /// result is processed as a stream rather than placed in a list.
    /// </summary>
    public interface IStreamingTypeDeserializer : ITypeSerializer
    {
        /// <summary>
        /// Create an <see cref="IJsonStreamReader"/> for parsing a <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream"><see cref="Stream"/> to parse.</param>
        /// <returns>The new <see cref="IJsonStreamReader"/>.</returns>
        IJsonStreamReader CreateJsonStreamReader(Stream stream);
    }
}
