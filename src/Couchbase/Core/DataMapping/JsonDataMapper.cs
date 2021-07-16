using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Serializers;

namespace Couchbase.Core.DataMapping
{

    /// <summary>
    /// A class for mapping an input stream of JSON to a Type T using a <see cref="Newtonsoft.Json.JsonTextReader"/> instance.
    /// </summary>
    internal class JsonDataMapper : IDataMapper
    {
        private readonly ITypeSerializer _serializer;

        public JsonDataMapper(ITypeSerializer serializer)
        {
            _serializer = serializer;
        }

        /// <inheritdoc />
        public T Map<T>(Stream stream) where T : class
        {
            return _serializer.Deserialize<T>(stream);
        }

        /// <inheritdoc />
        public ValueTask<T> MapAsync<T>(Stream stream, CancellationToken cancellationToken = default) where T : class
        {
            return _serializer.DeserializeAsync<T>(stream, cancellationToken);
        }
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
