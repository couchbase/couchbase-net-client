using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#nullable enable

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// Newtonsoft.Json implementation of <seealso cref="IJsonToken"/>
    /// which wraps a <seealso cref="JToken"/>.
    /// </summary>
    internal class NewtonsoftJsonToken : IJsonToken
    {
        private readonly JToken _token;
        private readonly JsonSerializer _deserializer;

        /// <summary>
        /// Creates a new NewtonsoftJsonToken.
        /// </summary>
        /// <param name="token">The <seealso cref="JToken"/> to wrap.</param>
        /// <param name="deserializer">Deserializer to use for <seealso cref="ToObject{T}"/> calls.</param>
        public NewtonsoftJsonToken(JToken token, JsonSerializer deserializer)
        {
            _token = token ?? throw new ArgumentNullException(nameof(token));
            _deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
        }

        /// <inheritdoc />
        public IJsonToken? this[string key]
        {
            get
            {
                var value = _token[key];

                return value != null ? new NewtonsoftJsonToken(_token[key], _deserializer) : null;
            }
        }

        /// <inheritdoc />
        public T ToObject<T>() => _token.ToObject<T>(_deserializer);

        /// <inheritdoc />
        public T Value<T>() => _token.Value<T>();

        /// <inheritdoc />
        public dynamic ToDynamic() => _token;
    }
}
