using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Serializers.SystemTextJson;

#nullable enable

namespace Couchbase.Core
{
    /// <summary>
    /// <see cref="JsonSerializerContext"/> capable of serializing and deserializing various internal types
    /// used by the Couchbase SDK to communicate with Couchbase Server.
    /// </summary>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
    [JsonSerializable(typeof(BucketConfig))]
    [JsonSerializable(typeof(ErrorMapDto))]
    [JsonSerializable(typeof(Hello.HelloKey))]
    [JsonSerializable(typeof(Manifest))]
    [JsonSerializable(typeof(TypeSerializerWrapper))]
    [JsonSerializable(typeof(Analytics.WarningData), TypeInfoPropertyName = "AnalyticsWarningData")]
    [JsonSerializable(typeof(Analytics.MetricsData), TypeInfoPropertyName = "AnalyticsMetricsData")]
    [JsonSerializable(typeof(Version.ClusterVersionProvider.Pools))]
    [JsonSerializable(typeof(Exceptions.KeyValue.KeyValueErrorContext))]
    [JsonSerializable(typeof(Exceptions.Analytics.AnalyticsErrorContext))]
    [JsonSerializable(typeof(Exceptions.Search.SearchErrorContext))]
    [JsonSerializable(typeof(Exceptions.View.ViewContextError))]
    [JsonSerializable(typeof(long))] // Used for expiry deserialization in GetResult
#if DEBUG
    [JsonSerializable(typeof(ServerFeatureSet))] // Only required for debug ToString implementation
#endif
    internal partial class InternalSerializationContext : JsonSerializerContext
    {
        /// <summary>
        /// The settings of <see cref="Default"/>, but with the relaxed encoder so that log-redaction
        /// tags survive serialization as literal &lt;ud&gt; markers rather than being emitted as
        /// unicode escape sequences. Tooling such as cblogredaction finds the tags textually and
        /// cannot match the escaped form.
        /// </summary>
        /// <remarks>
        /// Only error contexts should use this. The default encoder escapes '&lt;' and '&gt;' to keep
        /// JSON safe to embed in HTML; that does not apply to log output, but it does apply to
        /// anything that might be rendered in a page. The source-generated resolver is carried over
        /// from <see cref="Default"/>, so serialization stays trim- and AOT-safe.
        /// </remarks>
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

        private static SystemTextJsonSerializer? _defaultTypeSerializer;

        public static SystemTextJsonSerializer DefaultTypeSerializer
        {
            get
            {
                // First do a lock (and interlock) free check to see if the default serializer has been set.
                var serializer = _defaultTypeSerializer;
                if (serializer is not null)
                {
                    return serializer;
                }

                // Not set yet, or very recently set by another thread, so set using Interlocked.CompareExchange to ensure only a single instance is ever returned.
                // This is particularly important since the caller is likely long-lived and will cache this object for an extended period of time.
                return Interlocked.CompareExchange(ref _defaultTypeSerializer, SystemTextJsonSerializer.Create(Default), null) ?? _defaultTypeSerializer;
            }
        }

		[RequiresUnreferencedCode(DefaultSerializer.UnreferencedCodeMessage)]
        [RequiresDynamicCode(DefaultSerializer.RequiresDynamicCodeMessage)]
        public static string SerializeWithFallback<TValue>(TValue value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<TValue> jsonTypeInfo)
        {
            try
            {
                return System.Text.Json.JsonSerializer.Serialize<TValue>(value, jsonTypeInfo);
            }
            catch (NotSupportedException)
            {
                try
                {
                    using var memoryStream = new System.IO.MemoryStream();
                    Couchbase.Core.IO.Serializers.DefaultSerializer.Instance.Serialize(memoryStream, value);
                    return System.Text.Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
                }
                catch (Exception)
                {
                    // do nothing.  Re-throw the original exception.
                }

                throw;
            }
        }

        public static void SerializeWithFallback<TValue>(System.IO.Stream stream, TValue value,
            System.Text.Json.Serialization.Metadata.JsonTypeInfo<TValue> jsonTypeInfo,
            IFallbackTypeSerializerProvider fallbackTypeSerializerProvider)
        {
            try
            {
                System.Text.Json.JsonSerializer.Serialize<TValue>(stream, value, jsonTypeInfo);
            }
            catch (NotSupportedException)
            {
                try
                {
                    var fallbackSerializer = fallbackTypeSerializerProvider.Serializer;
                    if (fallbackSerializer is not null)
                    {
                        fallbackSerializer.Serialize(stream, value);
                        return;
                    }
                }
                catch (Exception)
                {
                    // do nothing.  Re-throw the original exception.
                }

                throw;
            }
        }
    }
}
