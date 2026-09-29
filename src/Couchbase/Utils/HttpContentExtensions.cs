using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Utils
{
    internal static class HttpContentExtensions
    {
#if !NET5_0_OR_GREATER
        /// <summary>
        /// Stands in for the <c>HttpContent.ReadAsStreamAsync(CancellationToken)</c> overload, which only
        /// exists on net5.0 and later - it cannot be referenced by cref here, because this file only compiles
        /// on the frameworks that lack it. On the modern target frameworks the instance method wins overload
        /// resolution and this is never called, so call sites can pass a token without a per-site #if.
        /// </summary>
        /// <remarks>
        /// The token cannot actually cancel the read here - netstandard offers no way to do so - but it keeps
        /// the call sites honest, and they get real cancellation on the frameworks that support it.
        /// </remarks>
        public static Task<Stream> ReadAsStreamAsync(this HttpContent content, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return content.ReadAsStreamAsync();
        }
#endif

        public static bool TryDeserialize<T>(this string jsonString, JsonTypeInfo<T> typeInfo,
            [NotNullWhen(true)] out T? result)
        {
            try
            {
                result = JsonSerializer.Deserialize(jsonString, typeInfo);
                return result is not null;
            }
            catch (JsonException)
            {
                result = default;
                return false;
            }
        }
    }
}
