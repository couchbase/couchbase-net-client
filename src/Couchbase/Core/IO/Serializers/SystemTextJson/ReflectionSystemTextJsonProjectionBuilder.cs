using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Serializers.SystemTextJson
{
    internal sealed class ReflectionSystemTextJsonProjectionBuilder : SystemTextJsonProjectionBuilder
    {
        [RequiresUnreferencedCode(ReflectionSystemTextJsonSerializer.SerializationUnreferencedCodeMessage)]
        [RequiresDynamicCode(ReflectionSystemTextJsonSerializer.SerializationDynamicCodeMessage)]
        public ReflectionSystemTextJsonProjectionBuilder(JsonSerializerOptions options, ILogger logger) : base(options, logger)
        {
        }

        /// <inheritdoc />
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = "This type may not be constructed without encountering a warning.")]
        [UnconditionalSuppressMessage("AOT", "IL3050",
            Justification = "This type may not be constructed without encountering a warning.")]
        public override T? ToObject<T>() where T : default
        {
            return RootNode.Deserialize<T>(Options);
        }

        /// <inheritdoc />
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = "This type may not be constructed without encountering a warning.")]
        [UnconditionalSuppressMessage("AOT", "IL3050",
            Justification = "This type may not be constructed without encountering a warning.")]
        public override T? ToPrimitive<T>() where T : default
        {
            return RootNode.First().Value.Deserialize<T>(Options);
        }
    }
}
