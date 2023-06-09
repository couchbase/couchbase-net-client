using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;

#nullable enable

namespace Couchbase.Core.IO.Serializers.SystemTextJson
{
    internal sealed class ReflectionSystemTextJsonStreamReader : SystemTextJsonStreamReader
    {
        private readonly JsonSerializerOptions _options;

        [RequiresUnreferencedCode(ReflectionSystemTextJsonSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(ReflectionSystemTextJsonSerializer.SerializationDynamicCodeMessage)]
        public ReflectionSystemTextJsonStreamReader(Stream stream, JsonSerializerOptions options)
            : base(stream, options)
        {
            _options = options;
        }

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = "This type may not be constructed without encountering a warning.")]
        [UnconditionalSuppressMessage("AOT", "IL3050",
            Justification = "This type may not be constructed without encountering a warning.")]
        public override T? Deserialize<T>(JsonElement element) where T : default =>
            element.Deserialize<T>(_options);

        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = "This type may not be constructed without encountering a warning.")]
        [UnconditionalSuppressMessage("AOT", "IL3050",
            Justification = "This type may not be constructed without encountering a warning.")]
        protected override T? Deserialize<T>(ref Utf8JsonReader reader) where T : default =>
            JsonSerializer.Deserialize<T>(ref reader, _options);
    }
}
