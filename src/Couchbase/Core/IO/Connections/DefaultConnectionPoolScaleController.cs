using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Default implementation of <see cref="IConnectionPoolScaleController"/>.
    /// </summary>
    internal class DefaultConnectionPoolScaleController : IConnectionPoolScaleController
    {
        private readonly IRedactor _redactor;
        private readonly ILogger<DefaultConnectionPoolScaleController> _logger;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private IConnectionPool? _connectionPool;
        private bool _disposed;

        /// <summary>
        /// How often the scale controller should monitor the pool.
        /// </summary>
        public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Connections may be closed if they are idle for at least this length of time.
        /// </summary>
        public TimeSpan IdleConnectionTimeout { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Amount of back pressure in the send queue necessary before a scale up is initiated.
        /// </summary>
        /// <remarks>
        /// If the number of queued items in <see cref="IConnectionPool.PendingSends"/> exceeds
        /// this value, the scale controller will choose to increase the size of the pool.
        /// </remarks>
        public int BackPressureThreshold { get; set; } = 8;

        public DefaultConnectionPoolScaleController(IRedactor redactor, ILogger<DefaultConnectionPoolScaleController> logger)
        {
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public void Start(IConnectionPool connectionPool)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DefaultConnectionPoolScaleController));
            }

            _connectionPool = connectionPool ?? throw new ArgumentNullException(nameof(connectionPool));

            _logger.LogDebug(
                "Starting connection pool monitor on {endpoint}, idle timeout {idleTimeout}, back pressure threshold {backPressureThreshold}",
                _redactor.SystemData(_connectionPool.EndPoint), IdleConnectionTimeout, BackPressureThreshold);

            Task.Run(MonitorAsync, _cts.Token);
        }

        protected virtual async Task MonitorAsync()
        {
            var cancellationToken = _cts.Token;
            if (_connectionPool == null)
            {
                // Should never occur, connection pool is set before MonitorAsync is invoked
                throw new InvalidOperationException();
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);

                    await using var freeze =
                        (await _connectionPool.FreezePoolAsync(cancellationToken).ConfigureAwait(false))
                        .ConfigureAwait(false);

                    await RunScalingLogic(_connectionPool).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Ignore
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Unhandled error in {nameof(DefaultConnectionPoolScaleController)}");
                }
            }

            _logger.LogDebug(
                "Stopping connection pool monitor on {endpoint}",
                _redactor.SystemData(_connectionPool.EndPoint));
        }

        protected async Task RunScalingLogic(IConnectionPool connectionPool)
        {
            var size = connectionPool.Size;

            if (size > connectionPool.MinimumSize)
            {
                // See if we should scale down

                if (connectionPool.GetConnections().Any(p => p.IdleTime >= IdleConnectionTimeout))
                {
                    _logger.LogInformation(
                        "Detected idle connections, scaling down connection pool {endpoint}",
                        _redactor.SystemData(connectionPool.EndPoint));

                    // We have at least one connection going idle, so scale down by 1 so it's gradual.
                    // We'll reevaluate on the next cycle if we need to scale down more.
                    await connectionPool.ScaleAsync(-1).ConfigureAwait(false);

                    // Don't do any further checks
                    return;
                }
            }

            if (size < connectionPool.MinimumSize)
            {
                // We should scale up
                // We'll reevaluate on the next cycle if we need to scale up more.

                _logger.LogInformation(
                    "Detected connection less than minimum, scaling up connection pool {endpoint}",
                     _redactor.SystemData(connectionPool.EndPoint));
                await connectionPool.ScaleAsync(1).ConfigureAwait(false);

                // Don't do any further checks
                return;
            }

            if (size < connectionPool.MaximumSize)
            {
                // See if we should scale up

                var backPressure = connectionPool.PendingSends;

                if (backPressure > BackPressureThreshold)
                {
                    // We should scale up
                    // We'll reevaluate on the next cycle if we need to scale up more.

                    _logger.LogInformation(
                        "Detected {count} back pressure, scaling up connection pool {endpoint}",
                        backPressure, _redactor.SystemData(connectionPool.EndPoint));
                    await connectionPool.ScaleAsync(1).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
