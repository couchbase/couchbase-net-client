using System;
using System.Buffers;
using Newtonsoft.Json;

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// Maps Newtonsoft.Json <see cref="IArrayPool{T}"/> to the .NET <see cref="ArrayPool{T}"/> so that
    /// the JSON serializer and deserializer can make use of the shared array pool.
    /// </summary>
    internal class JsonArrayPool : IArrayPool<char>
    {
        /// <summary>
        /// A shared instance of <see cref="JsonArrayPool"/> which uses <see cref="ArrayPool{T}.Shared"/>.
        /// </summary>
        public static JsonArrayPool Instance { get; } = new JsonArrayPool(ArrayPool<char>.Shared);

        private readonly ArrayPool<char> _pool;

        /// <summary>
        /// Creates a new JsonArrayPool.
        /// </summary>
        /// <param name="pool">The <see cref="ArrayPool{T}"/> to use.</param>
        /// <exception cref="ArgumentNullException"><paramref name="pool"/> is null.</exception>
        public JsonArrayPool(ArrayPool<char> pool)
        {
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        }

        /// <inheritdoc />
        public char[] Rent(int minimumLength)
        {
            return _pool.Rent(minimumLength);
        }

        /// <inheritdoc />
        public void Return(char[] array)
        {
            _pool.Return(array);
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
