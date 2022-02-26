using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.Sharding
{
    /// <summary>
    /// JSON converter optimized for vBucketMaps, which almost invariably contain 1024 elements,
    /// with each element being an array of the equal size. This converter reduces heap allocations
    /// by avoiding list resizing in the common case.
    /// </summary>
    internal sealed class VBucketMapJsonConverter : JsonConverter<short[][]>
    {
        private const int ExpectedVBucketCount = 1024;

        public override short[][] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                ThrowHelper.ThrowJsonException();
            }

            List<short[]>? list = null;
            short[][] arr = new short[ExpectedVBucketCount][];
            var index = 0;

            int expectedReplicaCount = 1;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    if (list is not null)
                    {
                        // This path is unlikely, reached if > 1024 elements
                        return list.ToArray();
                    }
                    else if (index == ExpectedVBucketCount)
                    {
                        return arr;
                    }
                    else
                    {
                        // This path is unlikely, reached if < 1024 elements
                        var result = new short[index][];
                        Array.Copy(arr, result, index);
                        return result;
                    }
                }

                if (index < ExpectedVBucketCount)
                {
                    arr[index] = ReadInnerList(ref reader, ref expectedReplicaCount);
                }
                else if (index == ExpectedVBucketCount)
                {
                    // We've exceeded our expected size, switch to a List<T>
                    list = new List<short[]>(arr);
                    list.Add(ReadInnerList(ref reader, ref expectedReplicaCount));
                }
                else
                {
                    Debug.Assert(list is not null);
                    list!.Add(ReadInnerList(ref reader, ref expectedReplicaCount));
                }

                index++;
            }

            ThrowHelper.ThrowJsonException();
            return null;
        }

        private short[] ReadInnerList(ref Utf8JsonReader reader, ref int expectedReplicaCount)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                ThrowHelper.ThrowJsonException();
            }

            List<short>? list = null;
            short[] arr = new short[expectedReplicaCount];
            var index = 0;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    if (list is not null)
                    {
                        // This path is unlikely after the first call, reached if > expectedReplicaCount elements

                        // Update the expectedReplicaCount for the next call
                        expectedReplicaCount = list.Count;

                        return list.ToArray();
                    }
                    else if (index == expectedReplicaCount)
                    {
                        return arr;
                    }
                    else
                    {
                        // This path is unlikely after the first call, reached if < expectedReplicaCount elements

                        // Update the expectedReplicaCount for the next call
                        expectedReplicaCount = index;

                        var result = new short[index];
                        Array.Copy(arr, result, index);
                        return result;
                    }
                }

                if (index < expectedReplicaCount)
                {
                    arr[index] = reader.GetInt16();
                }
                else if (index == expectedReplicaCount)
                {
                    // We've exceeded our expected size, switch to a List<T>
                    list = new List<short>(arr);
                    list.Add(reader.GetInt16());
                }
                else
                {
                    Debug.Assert(list is not null);
                    list!.Add(reader.GetInt16());
                }

                index++;
            }

            ThrowHelper.ThrowJsonException();
            return null;
        }

        public override void Write(Utf8JsonWriter writer, short[][] value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();

            foreach (var topElement in value)
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (topElement is null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    writer.WriteStartArray();

                    foreach (var innerElement in topElement)
                    {
                        writer.WriteNumberValue(innerElement);
                    }

                    writer.WriteEndArray();
                }
            }

            writer.WriteEndArray();
        }
    }
}
