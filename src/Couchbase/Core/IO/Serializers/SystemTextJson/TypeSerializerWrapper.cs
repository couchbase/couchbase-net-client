using System;
using System.Text.Json.Serialization;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.Serializers.SystemTextJson
{
    /// <summary>
    /// Wraps an object which should be serialized to JSON using an <see cref="ITypeSerializer"/>.
    /// This allows System.Text.Json serialization to override the behavior of a particular object
    /// and serialize it with the configured <see cref="ITypeSerializer"/>. In this way, some parts
    /// of the JSON object graph are serialized by our internal serializer and others by the custom serializer.
    /// </summary>
    /// <remarks>
    /// Note that some simple intrinsics, such as strings and numbers, may be serialized with the
    /// default serializer for performance reasons.
    /// </remarks>
    [JsonConverter(typeof(TypeSerializerWrapperConverter))]
    internal readonly struct TypeSerializerWrapper
    {
        public ITypeSerializer Serializer { get; }

        public object? Value { get; }

        public TypeSerializerWrapper(ITypeSerializer serializer, object? value)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (serializer is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(serializer));
            }

            Serializer = serializer;
            Value = value;
        }
    }
}
