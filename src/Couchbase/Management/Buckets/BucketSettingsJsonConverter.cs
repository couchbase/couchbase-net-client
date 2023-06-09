using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.KeyValue;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Management.Buckets
{
    internal class BucketSettingsJsonConverter : JsonConverter<BucketSettings>
    {
        public override BucketSettings Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                ThrowHelper.ThrowJsonException();
            }

            var settings = new BucketSettings();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return settings;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    ThrowHelper.ThrowJsonException();
                }

                var propertyName = reader.GetString();
                if (!reader.Read())
                {
                    ThrowHelper.ThrowJsonException();
                }

                switch (propertyName)
                {
                    case "name":
                        settings.Name = reader.GetString();
                        break;

                    case "maxTTL":
                        settings.MaxTtl = reader.GetInt32();
                        break;

                    case "bucketType":
                        settings.BucketType = ReadEnumIfNotNull<BucketType>(ref reader);
                        break;

                    case "replicaNumber":
                        settings.NumReplicas = reader.GetInt32();
                        break;

                    case "replicaIndex":
                        settings.ReplicaIndexes = reader.GetBoolean();
                        break;

                    case "conflictResolutionType":
                        settings.ConflictResolutionType = ReadEnumIfNotNull<ConflictResolutionType>(ref reader);
                        break;

                    case "compressionMode":
                        settings.CompressionMode = ReadEnumIfNotNull<CompressionMode>(ref reader);
                        break;

                    case "evictionPolicy":
                        settings.EvictionPolicy = ReadEnumIfNotNull<EvictionPolicyType>(ref reader);
                        break;

                    case "durabilityMinLevel":
                        settings.DurabilityMinimumLevel = ReadEnumIfNotNull<DurabilityLevel>(ref reader);
                        break;

                    case "storageBackend":
                        settings.StorageBackend = ReadEnumIfNotNull<StorageBackend>(ref reader);
                        break;

                    case "quota":
                        settings.RamQuotaMB = ReadRamQuota(ref reader);
                        break;

                    case "controllers":
                        settings.FlushEnabled = ReadFlushEnabled(ref reader);
                        break;
                }

                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                {
                    // The property value is a complex object we haven't read yet, we need to skip the contents to reach the next top-level property
                    if (!reader.TrySkip())
                    {
                        ThrowHelper.ThrowJsonException();
                    }
                }
            }

            // We didn't reach a closing curly brace
            ThrowHelper.ThrowJsonException();
            return null;
        }

        public override void Write(Utf8JsonWriter writer, BucketSettings value, JsonSerializerOptions options)
        {
            throw new NotSupportedException($"Serializing {nameof(BucketSettings)} as JSON is not supported.");
        }

        private long ReadRamQuota(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                ThrowHelper.ThrowJsonException();
            }

            var ramQuota = 0L;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return ramQuota;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    ThrowHelper.ThrowJsonException();
                }

                var propertyName = reader.GetString();
                if (!reader.Read())
                {
                    ThrowHelper.ThrowJsonException();
                }

                if (propertyName == "rawRAM")
                {
                    ramQuota = reader.GetInt64();
                }

                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                {
                    // The property value is a complex object we haven't read yet, we need to skip the contents to reach the next top-level property
                    if (!reader.TrySkip())
                    {
                        ThrowHelper.ThrowJsonException();
                    }
                }
            }

            // We didn't reach a closing curly brace
            ThrowHelper.ThrowJsonException();
            return 0;
        }

        private bool ReadFlushEnabled(ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                ThrowHelper.ThrowJsonException();
            }

            var flushEnabled = false;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return flushEnabled;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    ThrowHelper.ThrowJsonException();
                }

                var propertyName = reader.GetString();
                if (!reader.Read())
                {
                    ThrowHelper.ThrowJsonException();
                }

                if (propertyName == "flush")
                {
                    flushEnabled = reader.TokenType != JsonTokenType.Null;
                }

                if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
                {
                    // The property value is a complex object we haven't read yet, we need to skip the contents to reach the next top-level property
                    if (!reader.TrySkip())
                    {
                        ThrowHelper.ThrowJsonException();
                    }

                }
            }

            // We didn't reach a closing curly brace
            ThrowHelper.ThrowJsonException();
            return false;
        }

        private static T ReadEnumIfNotNull<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(ref Utf8JsonReader reader)
            where T : struct, Enum =>
            reader.TokenType switch
            {
                JsonTokenType.Null => default,
                JsonTokenType.String => EnumExtensions.TryGetFromDescription<T>(reader.GetString()!, out var value)
                    ? value
                    : default,
                _ => throw new JsonException()
            };
    }
}
