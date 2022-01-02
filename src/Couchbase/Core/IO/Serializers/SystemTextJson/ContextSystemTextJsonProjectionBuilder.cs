using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Serializers.SystemTextJson
{
    internal sealed class ContextSystemTextJsonProjectionBuilder : SystemTextJsonProjectionBuilder
    {
        private readonly JsonSerializerContext _context;

        public ContextSystemTextJsonProjectionBuilder(JsonSerializerContext context, ILogger logger) : base(context.Options, logger)
        {
            _context = context;
        }

        /// <inheritdoc />
        public override T? ToObject<T>() where T : default
        {
            var typeInfo = _context.GetTypeInfo<T>();

            return RootNode.Deserialize<T>(typeInfo);
        }

        /// <inheritdoc />
        public override T? ToPrimitive<T>() where T : default
        {
            var typeInfo = _context.GetTypeInfo<T>();

            return RootNode.First().Value.Deserialize<T>(typeInfo);
        }
    }
}
