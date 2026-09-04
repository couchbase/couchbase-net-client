using System;
using System.Collections.Generic;
using System.Threading;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Core.Exceptions;
using Couchbase.Management.Buckets;
using Couchbase.Management.Eventing;

#nullable enable

namespace Couchbase.Management
{
    /// <summary>
    /// Internal <see cref="JsonSerializerContext"/> used for management operations.
    /// </summary>
    /// <remarks>
    /// This is separate from the context used for general internal operations as an optimization.
    /// There is some small cost to the static constructor of a JsonSerializerContext, which scales based
    /// on the number of types included. Since management operations are more rarely used than others
    /// we keep them on a separate context.
    /// </remarks>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(BucketSettings))]
    [JsonSerializable(typeof(List<BucketSettings>))]
    [JsonSerializable(typeof(ManagementErrorContext))]
    [JsonSerializable(typeof(EventingFunctionErrorContext))]
    internal partial class ManagementSerializerContext : JsonSerializerContext
    {
        /// <summary>
        /// The settings of <see cref="Default"/>, but with the relaxed encoder so that
        /// log-redaction tags survive serialization as literal &lt;md&gt; markers rather than
        /// being emitted as unicode escape sequences. See
        /// <see cref="Couchbase.Core.InternalSerializationContext.RedactionSafeOptions"/>.
        /// </summary>
        private static JsonSerializerOptions? _redactionSafeOptions;

        internal static JsonSerializerOptions RedactionSafeOptions
        {
            get
            {
                // Deferred rather than a field initializer: Default is not yet constructed while
                // this class is running its own static initialization.
                var options = _redactionSafeOptions;
                if (options is not null)
                {
                    return options;
                }

                return Interlocked.CompareExchange(ref _redactionSafeOptions,
                    new JsonSerializerOptions(Default.Options)
                    {
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    }, null) ?? _redactionSafeOptions;
            }
        }
    }
}
