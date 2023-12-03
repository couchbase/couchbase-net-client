using System;
using System.Diagnostics.CodeAnalysis;
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
