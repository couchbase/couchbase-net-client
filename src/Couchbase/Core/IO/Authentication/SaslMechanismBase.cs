using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Authentication;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Retry;
using Couchbase.Diagnostics;
using Couchbase.KeyValue;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Authentication
{
    /// <summary>
    /// Base class for Simple Authentication and Security Layer (SASL) implementations.
    /// </summary>
    internal abstract class SaslMechanismBase : ISaslMechanism
    {
        public IRequestTracer Tracer { get; }
        protected ILogger? Logger;
        protected TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(2500);
        protected readonly IOperationConfigurator OperationConfigurator;

        protected SaslMechanismBase(IRequestTracer tracer, IOperationConfigurator operationConfigurator)
        {
            OperationConfigurator = operationConfigurator;
            Tracer = tracer;
        }

        /// <summary>
        /// The type of SASL mechanism to use: SCRAM-SHA1.
        /// </summary>
        public MechanismType MechanismType { get; internal set; }

        public abstract Task AuthenticateAsync(IConnection connection,
            CancellationToken cancellationToken = default);

        protected async Task<string> SaslStart(IConnection connection,  string message, IRequestSpan span, CancellationToken token)
        {
            using var childSpan = span.ChildSpan(OuterRequestSpans.ServiceSpan.Internal.SaslStart);
            using var authOp = new SaslStart
            {
                Key = MechanismType.GetDescription()!,
                Content = message,
                Timeout = Timeout,
                Span = childSpan
            };

            using var ctp = CancellationTokenPairSource.FromTimeout(Timeout, token);
            OperationConfigurator.Configure(authOp, SaslOptions.Instance);
            return await SendAsync(authOp, connection, token).ConfigureAwait(false);
        }

        protected async Task<string> SaslStep(IConnection connection, string message, IRequestSpan span, CancellationToken token)
        {
            using var childSpan = span.ChildSpan(OuterRequestSpans.ServiceSpan.Internal.SaslStep);
            using var op = new SaslStep()
            {
                Key = "SCRAM-SHA1",//MechanismType.GetDescription(),
                Content = message,
                Timeout = Timeout,
                Span = childSpan,
            };

            using var ctp = CancellationTokenPairSource.FromTimeout(Timeout, token);
            OperationConfigurator.Configure(op, SaslOptions.Instance);
            return await SendAsync(op, connection, ctp.TokenPair).ConfigureAwait(false);
        }

        protected async Task<string> SaslList(IConnection connection, IRequestSpan span, CancellationToken token)
        {
            using var op = new SaslList()
            {
                Timeout = Timeout,
                Span = span,
            };

            using var ctp = CancellationTokenPairSource.FromTimeout(Timeout, token);
            OperationConfigurator.Configure(op, SaslOptions.Instance);
            return await SendAsync(op, connection, ctp.TokenPair).ConfigureAwait(false);
        }

        protected async Task<T> SendAsync<T>(IOperation<T> op, IConnection connection, CancellationToken cancellationToken)
        {
            await op.SendAsync(connection, cancellationToken).ConfigureAwait(false);

            ResponseStatus status;

            using var ctp = CancellationTokenPairSource.FromTimeout(Timeout, cancellationToken);
            using (new OperationCancellationRegistration(op, ctp.TokenPair))
            {
                status = await op.Completed.ConfigureAwait(false);
            }

            if (status != ResponseStatus.Success && status != ResponseStatus.AuthenticationContinue)
            {
                throw new AuthenticationFailureException(
                    $"Cannot authenticate the user. Reason: {status}");
            }

            if (status == ResponseStatus.Success)
            {
                connection.EndpointState = EndpointState.Connected;
            }

            return op.GetValue()!;
        }

        /// <summary>
        /// Provides the transcoder override for SASL operations.
        /// </summary>
        protected class SaslOptions : ITranscoderOverrideOptions
        {
            public static SaslOptions Instance { get; } = new();

            public ITypeTranscoder? Transcoder { get; } = new LegacyTranscoder(); //required so that SASL strings are not JSON encoded

            internal IRetryStrategy? RetryStrategyValue { get; private set; }
            IRetryStrategy? IKeyValueOptions.RetryStrategy => RetryStrategyValue;

            public SaslOptions RetryStrategy(IRetryStrategy retryStrategy)
            {
                RetryStrategyValue = retryStrategy;
                return this;
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
