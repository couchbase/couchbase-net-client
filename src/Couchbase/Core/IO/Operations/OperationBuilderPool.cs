using System;
using System.Collections.Concurrent;
using System.Threading;
using Couchbase.Utils;

namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// Keeps a pool of <see cref="OperationBuilder"/> instances for reuse.
    /// </summary>
    internal class OperationBuilderPool
    {
        /// <summary>
        /// The current <see cref="OperationBuilderPool"/>,
        /// </summary>
        public static OperationBuilderPool Instance { get; } = new OperationBuilderPool();

        /// <summary>
        /// Returned operation builders with a capacity larger than this limit are disposed rather than retained.
        /// </summary>
        public int MaximumOperationBuilderCapacity { get; set; } = 1024 * 1024; // 1MB

        /// <summary>
        /// Maximum number of operation builders to retain, once this limit is reached any other returned builders are disposed.
        /// </summary>
        public int MaximumRetainedOperationBuilders { get; set; } = 32;

        private readonly ConcurrentBag<OperationBuilder> _builders = new ConcurrentBag<OperationBuilder>();
        private volatile int _builderCount;

        /// <summary>
        /// Rents an <see cref="OperationBuilder"/> from the pool, creating a new one if none is available.
        /// </summary>
        /// <returns>The <see cref="OperationBuilder"/>.</returns>
        /// <exception cref="ArgumentNullException">Converter is null.</exception>
        [NotNull]
        public OperationBuilder Rent()
        {
            if (!_builders.TryTake(out var builder))
            {
                Interlocked.Increment(ref _builderCount);
                return new OperationBuilder();
            }

            return builder;
        }

        /// <summary>
        /// Returns an <see cref="OperationBuilder"/> to the pool.
        /// </summary>
        /// <param name="builder">The <see cref="OperationBuilder"/> to return.</param>
        /// <exception cref="ArgumentNullException">The builder is null.</exception>
        public void Return([NotNull] OperationBuilder builder)
        {
            if (builder == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(builder));
            }

            if (builder.Capacity > MaximumOperationBuilderCapacity ||
                _builderCount >= MaximumRetainedOperationBuilders)
            {
                Interlocked.Decrement(ref _builderCount);
                builder.Dispose();
                return;
            }

            builder.Reset();
            _builders.Add(builder);
        }
    }
}
