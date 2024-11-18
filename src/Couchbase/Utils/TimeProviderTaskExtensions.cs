using Couchbase.Core.IO.Operations;
using Couchbase.Utils;

namespace System.Threading.Tasks
{
    internal static class TimeProviderTaskExtensions
    {
#if NET8_0_OR_GREATER

        // These extensions are provided by Microsoft.Bcl.TimeProvider and have complex implementations
        // for downlevel runtimes but simply forward to Task.Delay and CancellationTokenSource in .NET 8.
        // In order to avoid the dependency on Microsoft.Bcl.TimeProvider, we can implement the simple forms
        // here for .NET 8 and later.

        public static Task Delay(this TimeProvider timeProvider, TimeSpan delay, CancellationToken cancellationToken = default) =>
            Task.Delay(delay, timeProvider, cancellationToken);

        public static CancellationTokenSource CreateCancellationTokenSource(this TimeProvider timeProvider, TimeSpan delay) =>
            new(delay, timeProvider);

#endif

        public static CancellationTokenPairSource CreateCancellationTokenPairSource(this TimeProvider timeProvider, TimeSpan delay, CancellationToken externalToken)
        {
#if NET8_0_OR_GREATER
            // In .NET 8, the CancellationTokenPairSource can accept a TimeProvider directly

            return new CancellationTokenPairSource(delay, externalToken, timeProvider);
#else
            if (timeProvider is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(timeProvider));
            }

            if (delay != Timeout.InfiniteTimeSpan && delay < TimeSpan.Zero)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(delay));
            }

            if (timeProvider == TimeProvider.System)
            {
                return new CancellationTokenPairSource(delay, externalToken);
            }

            // Received a custom TimeProvider, so handle the delay using a timer from the TimeProvider

            var ctps = new CancellationTokenPairSource(externalToken);

            ITimer timer = timeProvider.CreateTimer(static s =>
            {
                try
                {
                    ((CancellationTokenPairSource)s!).Cancel();
                }
                catch (ObjectDisposedException) { }
            }, ctps, delay, Timeout.InfiniteTimeSpan);

            ctps.Token.Register(static t => ((ITimer)t!).Dispose(), timer);
            return ctps;
#endif
        }

    }
}

