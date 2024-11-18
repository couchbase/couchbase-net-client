using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations;
using Microsoft.Extensions.ObjectPool;

#nullable enable

namespace Couchbase.Utils
{
    /// <summary>
    /// For .NET 6, provides a pool of reusable <see cref="CancellationTokenPairSource"/>. This is made
    /// possible by the new TryReset API available. For older frameworks this class is effectively a noop.
    /// </summary>
    internal sealed class CancellationTokenPairSourcePool : IDisposable
    {
        /// <summary>
        /// Shared instance of <see cref="CancellationTokenPairSourcePool"/>.
        /// </summary>
        public static CancellationTokenPairSourcePool Shared { get; } = new();

        #if NET6_0_OR_GREATER

        private readonly ObjectPool<CancellationTokenPairSource> _pool =
            ObjectPool.Create(new CancellationTokenPairSourcePoolPolicy());

        public CancellationTokenPairSource Rent() => _pool.Get();

        public CancellationTokenPairSource Rent(TimeSpan delay, CancellationToken externalToken)
        {
            var cts = Rent();
            cts.ExternalToken = externalToken;
            cts.CancelAfter(delay);
            return cts;
        }

        public void Return(CancellationTokenPairSource cts) => _pool.Return(cts);

        // Since we're making a pool for a class that implements IDisposable, the pool
        // will also implement IDisposable. Disposing will dispose of any instances retained
        // in the pool.
        public void Dispose() => (_pool as IDisposable)?.Dispose();

        private sealed class CancellationTokenPairSourcePoolPolicy : PooledObjectPolicy<CancellationTokenPairSource>
        {
            public override CancellationTokenPairSource Create() => new();

            // ReSharper disable once MemberHidesStaticFromOuterClass
            public override bool Return(CancellationTokenPairSource obj) =>
                // It is imperative that obj be typed as CancellationTokenPairSource here, not
                // CancellationTokenSource, so that the new TryReset method is called and not the
                // shadowed version on CancellationTokenSource.
                obj.TryReset();
        }

        #else

        public CancellationTokenPairSource Rent() => new();

        public CancellationTokenPairSource Rent(TimeSpan delay, CancellationToken externalToken) =>
            new(delay, externalToken);

        public void Return(CancellationTokenPairSource cts) => cts.Dispose();

        public void Dispose()
        {
        }

        #endif

        public CancellationTokenPairSource Rent(TimeProvider timeProvider, TimeSpan delay, CancellationToken externalToken)
        {
            if (timeProvider == TimeProvider.System)
            {
                // Using system time provider, so we can use the pool and the built-in CancelAfter
                return Rent(delay, externalToken);
            }

            // Can't use the pool when unit testing with a custom time provider, it isn't possible
            // to change the time provider after constructing the CTS. Also, TryReset will always
            // return false. Just create a new CTS each time.
            return timeProvider.CreateCancellationTokenPairSource(delay, externalToken);
        }
    }
}
