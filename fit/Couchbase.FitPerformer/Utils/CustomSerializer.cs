using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;
using Newtonsoft.Json.Linq;

namespace Couchbase.FitPerformer.Utils
{
    // A "tracer" serializer used by the ExtSerialization FIT tests.
    //
    // On serialize it wraps content as {"content": <obj>, "Serialized": true}, so the test can
    // confirm - by reading the raw stored bytes - that the SDK serialized through this serializer.
    //
    // On deserialize it unwraps that envelope and stamps "Serialized": false into the content, so
    // the test can confirm the read also went through this serializer.
    //
    // All overloads (ReadOnlyMemory, Stream, async) must behave identically: the SDK content path
    // (JsonTranscoder.Decode -> DeserializeAsJson) calls the ReadOnlyMemory overload, while other
    // paths may use the Stream overloads.
    public class CustomSerializer : ITypeSerializer
    {
        public T Deserialize<T>(ReadOnlyMemory<byte> buffer)
        {
            return Unwrap<T>(buffer);
        }

        public T Deserialize<T>(Stream stream)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return Unwrap<T>(ms.ToArray());
        }

        public async ValueTask<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            return Unwrap<T>(ms.ToArray());
        }

        public void Serialize(Stream stream, object obj)
        {
            using var jsonUtf8Writer = new Utf8JsonWriter(stream);
            JsonSerializer.Serialize(jsonUtf8Writer, Wrap(obj));
        }

        public ValueTask SerializeAsync(Stream stream, object obj, CancellationToken cancellationToken = default)
        {
            return new ValueTask(JsonSerializer.SerializeAsync(stream, Wrap(obj), (JsonSerializerOptions)null, cancellationToken));
        }

        // Wrap content in the tracer envelope, marking that it was serialized by this serializer.
        private static CustomJSON<object> Wrap(object obj)
        {
            if (obj is JToken jtoken)
            {
                // System.Text.Json does not serialize Newtonsoft JTokens correctly, so round-trip
                // through their JSON text into a System.Text.Json-friendly representation first.
                obj = JsonSerializer.Deserialize<object>(jtoken.ToString(Newtonsoft.Json.Formatting.None));
            }

            return new CustomJSON<object>(obj, true);
        }

        // Unwrap the tracer envelope. For JToken targets, stamp Serialized=false into the content so
        // the test can confirm deserialization ran through this serializer.
        private static T Unwrap<T>(ReadOnlyMemory<byte> buffer)
        {
            using var doc = JsonDocument.Parse(buffer);
            var root = doc.RootElement;

            var isWrapped = root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("Serialized", out _)
                && root.TryGetProperty("content", out _);
            var content = isWrapped ? root.GetProperty("content") : root;

            if (typeof(T) == typeof(JObject) || typeof(T) == typeof(JToken))
            {
                var token = JToken.Parse(content.GetRawText());
                if (token is JObject obj)
                {
                    obj["Serialized"] = false;
                }

                return (T)(object)token;
            }

            return content.Deserialize<T>();
        }
    }
}
