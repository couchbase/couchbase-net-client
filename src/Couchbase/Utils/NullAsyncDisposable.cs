using System;
using System.Threading.Tasks;

namespace Couchbase.Utils
{
    /// <summary>
    /// Implementation of <see cref="IAsyncDisposable"/> which does nothing.
    /// </summary>
    internal class NullAsyncDisposable : IAsyncDisposable
    {
        /// <summary>
        /// Reusable static instance of <see cref="NullAsyncDisposable"/>.
        /// </summary>
        public static NullAsyncDisposable Instance { get; } = new NullAsyncDisposable();

        /// <inheritdoc />
        public ValueTask DisposeAsync() => default;
    }
}
