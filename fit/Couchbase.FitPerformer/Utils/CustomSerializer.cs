using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;

namespace Couchbase.FitPerformer.Utils
{
    public class CustomSerializer : ITypeSerializer
    {
        public T Deserialize<T>(ReadOnlyMemory<byte> buffer)
        {
            return JsonSerializer.Deserialize<T>(buffer.Span);
        }

        public T Deserialize<T>(Stream stream)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var span = new ReadOnlySpan<byte>(ms.GetBuffer()).Slice(0, (int)ms.Length);
            var wrapped = JsonSerializer.Deserialize<CustomJSON<T>>(span);
            return wrapped.content;
        }

        public async ValueTask<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
        {
            var wrapped = await JsonSerializer.DeserializeAsync<CustomJSON<T>>(stream, (JsonSerializerOptions)null, cancellationToken);
            return wrapped.content;
        }

        // This is the defualt behaviour of the custom serializer from the docs
        public void Serialize(Stream stream, object obj)
        {
            if (obj is Newtonsoft.Json.Linq.JObject jobj)
            {
                // special case.  System.Text.Json.JsonSerializer does not play well with JObject.
                string json = jobj.ToString();
                obj = JsonSerializer.Deserialize<object>(json);
            }

            var customJson = new CustomJSON<object>(obj, true);
            using var jsonUtf8Writer = new Utf8JsonWriter(stream);
            JsonSerializer.Serialize(jsonUtf8Writer, customJson);
        }

        public ValueTask SerializeAsync(Stream stream, object obj, CancellationToken cancellationToken = default)
        {
            var customJson = new CustomJSON<object>(obj, true);
            return new ValueTask(JsonSerializer.SerializeAsync(stream, customJson, (JsonSerializerOptions)null, cancellationToken));
        }
    }
}
