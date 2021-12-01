using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Couchbase.Utils;

namespace Couchbase.Core.IO.Serializers.SystemTextJson
{
    /// <summary>
    /// Helpers for working with <see cref="JsonSerializerContext"/>.
    /// </summary>
    internal static class ContextHelpers
    {
        /// <summary>
        /// Gets the <see cref="JsonTypeInfo{T}" /> for <typeparamref name="T"/> from the <paramref name="context"/>,
        /// or throws an exception if it is not present.
        /// </summary>
        /// <typeparam name="T">Type to lookup.</typeparam>
        /// <param name="context">The <see cref="JsonSerializerContext"/>.</param>
        /// <returns>The <see cref="JsonTypeInfo{T}"/> from the context.</returns>
        /// <exception cref="InvalidOperationException">The type info was not found.</exception>
        public static JsonTypeInfo<T> GetTypeInfo<T>(this JsonSerializerContext context)
        {
            if (context.TryGetTypeInfo<T>(out var typeInfo))
            {
                return typeInfo;
            }

            ThrowNoMetadataForType(typeof(T));
            return null!;
        }

        /// <summary>
        /// Gets the <see cref="JsonTypeInfo{T}" /> for <typeparamref name="T"/> from the <paramref name="context"/>.
        /// </summary>
        /// <typeparam name="T">Type to lookup.</typeparam>
        /// <param name="context">The <see cref="JsonSerializerContext"/>.</param>
        /// <param name="typeInfo">The <see cref="JsonTypeInfo{T}"/> from the context.</param>
        /// <returns>True if the <see cref="JsonTypeInfo{T}"/> was found.</returns>
        public static bool TryGetTypeInfo<T>(this JsonSerializerContext context, [MaybeNullWhen(false)] out JsonTypeInfo<T> typeInfo)
        {
            if (context == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(context));
            }

            if (context.GetTypeInfo(typeof(T)) is JsonTypeInfo<T> newTypeInfo)
            {
                typeInfo = newTypeInfo;
                return true;
            }

            typeInfo = null;
            return false;
        }

        [DoesNotReturn]
        internal static void ThrowNoMetadataForType(Type type) =>
            ThrowHelper.ThrowInvalidOperationException(
                $"No serialization metadata is present for the type '{type}'. Ensure you are using a SystemTextJsonSerializer created with plain JsonSerializerOptions or the JsonSerializerContext has metadata for the type.");
    }
}
