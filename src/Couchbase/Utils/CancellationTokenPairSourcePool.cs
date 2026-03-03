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

        private CancellationTokenPairSource RentRaw() => _pool.Get();

        public CancellationTokenPairSourceWrapper Rent() => new(this, RentRaw());

        public CancellationTokenPairSourceWrapper Rent(TimeSpan delay, CancellationToken externalToken)
        {
            var cts = RentRaw();
            cts.ExternalToken = externalToken;
            cts.CancelAfter(delay);
            return new CancellationTokenPairSourceWrapper(this, cts);
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

        private CancellationTokenPairSource RentRaw() => new();

        public CancellationTokenPairSourceWrapper Rent() => new(this, RentRaw());

        public CancellationTokenPairSourceWrapper Rent(TimeSpan delay, CancellationToken externalToken) =>
            new(this, new(delay, externalToken));

        public void Return(CancellationTokenPairSource cts) => cts.Dispose();

        public void Dispose()
        {
        }

        #endif

        public CancellationTokenPairSourceWrapper Rent(TimeProvider timeProvider, TimeSpan delay, CancellationToken externalToken)
        {
            if (timeProvider == TimeProvider.System)
            {
                // Using system time provider, so we can use the pool and the built-in CancelAfter
                return Rent(delay, externalToken);
            }

            // Can't use the pool when unit testing with a custom time provider, it isn't possible
            // to change the time provider after constructing the CTS. Also, TryReset will always
            // return false. Just create a new CTS each time.
            return new CancellationTokenPairSourceWrapper(null, timeProvider.CreateCancellationTokenPairSource(delay, externalToken));
        }
    }

    /// <summary>
    /// A disposable wrapper for a <see cref="CancellationTokenPairSource"/> rented from a <see cref="CancellationTokenPairSourcePool"/>.
    /// </summary>
    internal readonly struct CancellationTokenPairSourceWrapper : IDisposable
    {
        private readonly CancellationTokenPairSourcePool? _pool;
        private readonly CancellationTokenPairSource? _source;

        public CancellationToken Token => _source?.Token ?? default;
        public CancellationToken ExternalToken => _source?.ExternalToken ?? default;
        public CancellationTokenPair TokenPair => _source?.TokenPair ?? default;
        public bool IsExternalCancellation => _source?.IsExternalCancellation ?? false;
        public bool IsInternalCancellation => _source?.IsInternalCancellation ?? false;

        public CancellationTokenPairSourceWrapper(CancellationTokenPairSourcePool? pool, CancellationTokenPairSource source)
        {
            _pool = pool;
            _source = source;
        }

        public void Dispose()
        {
            if (_pool != null)
            {
                if (_source != null)
                {
                    _pool.Return(_source);
                }
            }
            else
            {
                _source?.Dispose();
            }
        }
    }
}
