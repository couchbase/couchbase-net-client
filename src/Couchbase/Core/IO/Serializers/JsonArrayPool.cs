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
