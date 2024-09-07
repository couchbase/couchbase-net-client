using System;
using System.Threading;
using Microsoft.Extensions.ObjectPool;

#nullable enable

namespace Couchbase.Utils
{
    /// <summary>
    /// For .NET 6, provides a pool of reusable <see cref="CancellationTokenSource"/>. This is made
    /// possible by the new TryReset API available. For older frameworks this class is effectively a noop.
    /// </summary>
    internal sealed class CancellationTokenSourcePool : IDisposable
    {
        /// <summary>
        /// Shared instance of <see cref="CancellationTokenSourcePool"/>.
        /// </summary>
        public static CancellationTokenSourcePool Shared { get; } = new();

        #if NET6_0_OR_GREATER

        private readonly ObjectPool<CancellationTokenSource> _pool =
            ObjectPool.Create(new CancellationTokenSourcePoolPolicy());

        public CancellationTokenSource Rent() => _pool.Get();

        public CancellationTokenSource Rent(TimeSpan delay)
        {
            var cts = Rent();
            cts.CancelAfter(delay);
            return cts;
        }

        public void Return(CancellationTokenSource cts) => _pool.Return(cts);

        // Since we're making a pool for a class that implements IDisposable, the pool
        // will also implement IDisposable. Disposing will dispose of any instances retained
        // in the pool.
        public void Dispose() => (_pool as IDisposable)?.Dispose();

        private class CancellationTokenSourcePoolPolicy : PooledObjectPolicy<CancellationTokenSource>
        {
            public override CancellationTokenSource Create() => new();

            // ReSharper disable once MemberHidesStaticFromOuterClass
            public override bool Return(CancellationTokenSource obj) => obj.TryReset();
        }

        #else

        public CancellationTokenSource Rent() => new();

        public CancellationTokenSource Rent(TimeSpan delay) =>
            new(delay);

        public void Return(CancellationTokenSource cts) => cts.Dispose();

        public void Dispose()
        {
        }

        #endif
    }
}
