using System;
using System.Diagnostics;
using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Nodes;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.IO.Serializers.SystemTextJson
{
    internal class SystemTextJsonToken : IJsonToken
    {
        private readonly JsonElement _element;
        private readonly SystemTextJsonStreamReader _streamReader;

        /// <summary>
        /// Creates a new NewtonsoftJsonToken.
        /// </summary>
        /// <param name="element">The <seealso cref="JsonElement"/> to wrap.</param>
        /// <param name="streamReader"><see cref="SystemTextJsonStreamReader"/> to use for deserialization.</param>
        public SystemTextJsonToken(JsonElement element, SystemTextJsonStreamReader streamReader)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (streamReader == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(streamReader));
            }

            _element = element;
            _streamReader = streamReader;
        }

        /// <inheritdoc />
        public IJsonToken? this[string key]
        {
            get
            {
                if (_element.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null)
                {
                    return new SystemTextJsonToken(value, _streamReader);
                }

                return null;
            }
        }

        /// <inheritdoc />
        public T ToObject<T>() => _streamReader.Deserialize<T>(_element)!;

        /// <inheritdoc />
        public T Value<T>()
        {
            if (_element.TryGetValue<T>(out var value))
            {
                return value!;
            }

            ThrowHelper.ThrowInvalidOperationException($"Unable to convert {_element.ValueKind} to {typeof(T)}.");
            return default!;
        }

        /// <inheritdoc />
        public dynamic ToDynamic() => new ExpandoObject();
    }
}
