using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

#nullable enable

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// Implementation of <see cref="IProjectionBuilder"/> for <see cref="DefaultSerializer"/>.
    /// </summary>
    [RequiresUnreferencedCode(DefaultSerializer.UnreferencedCodeMessage)]
    [RequiresDynamicCode(DefaultSerializer.RequiresDynamicCodeMessage)]
    internal class NewtonsoftProjectionBuilder : IProjectionBuilder
    {
        private readonly DefaultSerializer _serializer;
        private readonly ILogger _logger;
        private readonly JObject _root = new();

        /// <summary>
        /// Creates a new NewtonsoftProjectionBuilder.
        /// </summary>
        /// <param name="serializer">DefaultSerializer to use when deserializing content.</param>
        /// <param name="logger">Logger for recording warnings and errors.</param>
        public NewtonsoftProjectionBuilder(DefaultSerializer serializer, ILogger logger)
        {
            _serializer = serializer;
            _logger = logger;
        }

        /// <inheritdoc />
        public void AddPath(string path, ReadOnlyMemory<byte> specValue)
        {
            var content = _serializer.Deserialize<JToken>(specValue)!;
            var projection = CreateProjection(path, content);

            try
            {
                _root.Add(projection.First);
            }
            catch (Exception e)
            {
                //these are cases where a root attribute is already mapped
                //for example "attributes" and "attributes.hair" will cause exceptions
                _logger.LogInformation(e, "Deserialization failed.");
            }
        }

        /// <inheritdoc />
        public void AddChildren(IReadOnlyCollection<string> children, ReadOnlyMemory<byte> specValue)
        {
            foreach (var child in _serializer.Deserialize<JToken>(specValue)!.Children())
            {
                if (children.Contains(child.Path))
                {
                    _root.Add(child);
                }
            }
        }

        /// <inheritdoc />
        public T ToObject<T>() => _root.ToObject<T>()!;

        /// <inheritdoc />
        public T ToPrimitive<T>() => (_root.First!.ToObject<T>())!;

        private static void BuildPath(JToken token, string name, JToken? content = null)
        {
            foreach (var child in token.Children())
            {
                if (child is JValue value)
                {
                    value.Replace(new JObject(new JProperty(name, content)));
                    break;
                }
                BuildPath(child, name, content);
            }
        }

        private static JObject CreateProjection(string path, JToken content)
        {
            var elements = path.Split('.');
            var projection = new JObject();
            if (elements.Length == 1)
            {
                projection.Add(new JProperty(elements[0], content));
            }
            else
            {
                for (var i = 0; i < elements.Length; i++)
                {
                    if (projection.Last != null)
                    {
                        if (i == elements.Length - 1)
                        {
                            BuildPath(projection, elements[i], content);
                            continue;
                        }

                        BuildPath(projection, elements[i]);
                        continue;
                    }

                    projection.Add(new JProperty(elements[i], (object?)null));
                }
            }

            return projection;
        }
    }
}
