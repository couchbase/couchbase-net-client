using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.Bootstrapping
{
    /// <inheritdoc />
    internal class Bootstrapper : IBootstrapper
    {
        private readonly CancellationTokenSource _tokenSource;
        private readonly ILogger<Bootstrapper> _logger;
        private volatile bool _disposed;

        public Bootstrapper(ILogger<Bootstrapper> logger) : this(new CancellationTokenSource(), logger) { }

        public Bootstrapper(CancellationTokenSource tokenSource, ILogger<Bootstrapper> logger)
        {
            _tokenSource = tokenSource ?? throw new ArgumentNullException(nameof(tokenSource));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public TimeSpan SleepDuration { get; set; }

        /// <inheritdoc />
        public void Start(IBootstrappable subject)
        {
            var token = _tokenSource.Token;
            Task.Run(async () =>
            {
                token.ThrowIfCancellationRequested();
                while (!token.IsCancellationRequested)
                {
                    _logger.LogTrace("The subject is bootstrapped: {isBootstrapped}", subject.IsBootstrapped);
                    if (!subject.IsBootstrapped)
                    {
                        _logger.LogDebug("The subject is not bootstrapped.");
                        try
                        {
                            await subject.BootStrapAsync().ConfigureAwait(false);
                            subject.DeferredExceptions.Clear();

                            _logger.LogDebug("The subject has successfully bootstrapped.");
                        }
                        catch (Exception e)
                        {
                            _logger.LogDebug("The subject has not successfully bootstrapped.", e);

                            //catch any errors not caught in the bootstrap catch clause
                            subject.DeferredExceptions.Add(e);
                        }
                    }

                    await Task.Delay(SleepDuration, token).ConfigureAwait(false);
                }
            }, token);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _tokenSource?.Dispose();
        }
    }
}
