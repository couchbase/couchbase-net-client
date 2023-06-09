using System;
using System.Diagnostics.CodeAnalysis;
using Couchbase.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#nullable enable

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// Newtonsoft.Json implementation of <seealso cref="IJsonToken"/>
    /// which wraps a <seealso cref="JToken"/>.
    /// </summary>
    [RequiresUnreferencedCode(DefaultSerializer.UnreferencedCodeMessage)]
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
            if (token == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(token));
            }
            if (deserializer == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(deserializer));
            }

            _token = token;
            _deserializer = deserializer;
        }

        /// <inheritdoc />
        public IJsonToken? this[string key]
        {
            get
            {
                var value = _token[key];

                return value != null ? new NewtonsoftJsonToken(value, _deserializer) : null;
            }
        }

        /// <inheritdoc />
        public T ToObject<T>() => _token.ToObject<T>(_deserializer)!;

        /// <inheritdoc />
        public T Value<T>() => _token.Value<T>()!;

        /// <inheritdoc />
        public dynamic ToDynamic() => _token;
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
