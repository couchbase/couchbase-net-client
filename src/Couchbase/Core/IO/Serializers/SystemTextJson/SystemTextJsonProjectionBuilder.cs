using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Serializers.SystemTextJson
{
    internal abstract class SystemTextJsonProjectionBuilder : IProjectionBuilder
    {
        protected JsonSerializerOptions Options { get; }
        protected ILogger Logger { get; }
        protected JsonObject RootNode { get; }

        protected SystemTextJsonProjectionBuilder(JsonSerializerOptions options, ILogger logger)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            RootNode = new JsonObject(new JsonNodeOptions
            {
                PropertyNameCaseInsensitive = options.PropertyNameCaseInsensitive
            });
        }

        /// <inheritdoc />
        public void AddPath(string path, ReadOnlyMemory<byte> specValue)
        {
            using var document = JsonDocument.Parse(specValue, new JsonDocumentOptions
            {
                AllowTrailingCommas = Options.AllowTrailingCommas,
                CommentHandling = Options.ReadCommentHandling,
                MaxDepth = Options.MaxDepth
            });

            AddChild(path, document.RootElement.Clone());
        }

        /// <inheritdoc />
        public void AddChildren(IReadOnlyCollection<string> children, ReadOnlyMemory<byte> specValue)
        {
            using var document = JsonDocument.Parse(specValue, new JsonDocumentOptions
            {
                AllowTrailingCommas = Options.AllowTrailingCommas,
                CommentHandling = Options.ReadCommentHandling,
                MaxDepth = Options.MaxDepth
            });

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Expected a JSON object");
            }

            foreach (var child in children)
            {
                if (document.RootElement.TryGetProperty(child, out var property))
                {
                    AddChild(RootNode, child, property.Clone());
                }
            }
        }

        /// <inheritdoc />
        public abstract T? ToObject<T>();

        /// <inheritdoc />
        public abstract T? ToPrimitive<T>();

        private void AddChild(string path, JsonElement element)
        {
            AddChild(RootNode, path.Split('.'), element);
        }

        private void AddChild(JsonObject node, ReadOnlySpan<string> path, JsonElement element)
        {
            if (path.Length == 1)
            {
                AddChild(node, path[0], element);
            }
            else
            {
                JsonObject childObject;
                if (node.TryGetPropertyValue(path[0], out var propertyValue) && propertyValue != null)
                {
                    childObject = propertyValue.AsObject();
                }
                else
                {
                    childObject = new JsonObject();
                    node[path[0]] = childObject;
                }

                AddChild(childObject, path.Slice(1), element);
            }
        }

        private void AddChild(JsonObject node, string propertyName, JsonElement element)
        {
            var options = new JsonNodeOptions
            {
                PropertyNameCaseInsensitive = Options.PropertyNameCaseInsensitive
            };

            JsonNode? childNode = element.ValueKind == JsonValueKind.Object
                ? JsonObject.Create(element, options)
                : element.ValueKind == JsonValueKind.Array
                    ? JsonArray.Create(element, options)
                    : JsonValue.Create(element, options);

            node[propertyName] = childNode;
        }
    }
}
