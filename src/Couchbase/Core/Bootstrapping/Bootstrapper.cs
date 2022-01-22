using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.Bootstrapping
{
    /// <inheritdoc />
    internal class Bootstrapper : IBootstrapper
    {
        private readonly CancellationTokenSource _tokenSource;
        private readonly ILogger<Bootstrapper> _logger;
        private volatile int _disposed;

        public Bootstrapper(ILogger<Bootstrapper> logger) : this(new CancellationTokenSource(), logger) { }

        public Bootstrapper(CancellationTokenSource tokenSource, ILogger<Bootstrapper> logger)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (tokenSource == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(tokenSource));
            }
            if (logger == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(logger));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            _tokenSource = tokenSource;
            _logger = logger;
        }

        /// <inheritdoc />
        public TimeSpan SleepDuration { get; set; }

        /// <inheritdoc />
        public void Start(IBootstrappable subject)
        {
            // Ensure that we don't flow the ExecutionContext into the long running task below
            bool restoreFlow = false;
            try
            {
                if (ExecutionContext.IsFlowSuppressed())
                {
                    ExecutionContext.SuppressFlow();
                    restoreFlow = true;
                }

                _ = Execute(subject);
            }
            finally
            {
                if (restoreFlow)
                {
                    ExecutionContext.RestoreFlow();
                }
            }
        }

        private async Task Execute(IBootstrappable subject)
        {
            var token = _tokenSource.Token;
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
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _tokenSource.Cancel();
                _tokenSource.Dispose();
            }
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
