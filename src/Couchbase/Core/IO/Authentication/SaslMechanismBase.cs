using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Authentication;
using Couchbase.Core.IO.Transcoders;
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
        protected ILogger? Logger;
        protected ITypeTranscoder? Transcoder;
        private TimeSpan Timeout { get; set; } = TimeSpan.FromMilliseconds(2500);

        /// <summary>
        /// The type of SASL mechanism to use: SCRAM-SHA1.
        /// </summary>
        public MechanismType MechanismType { get; internal set; }

        public abstract Task AuthenticateAsync(IConnection connection,
            CancellationToken cancellationToken = default);

        protected async Task<string> SaslStart(IConnection connection,  string message, CancellationToken token)
        {
            using var authOp = new SaslStart
            {
                Key = MechanismType.GetDescription(),
                Content = message,
                Transcoder = Transcoder,
                Timeout = Timeout
            };
            return await SendAsync(authOp, connection, token).ConfigureAwait(false);
        }

        protected async Task<string> SaslStep(IConnection connection, string message, CancellationToken token)
        {
            using var op = new SaslStep
            {
                Key = "SCRAM-SHA1",//MechanismType.GetDescription(),
                Content = message,
                Transcoder = Transcoder,
                Timeout = Timeout
            };
            return await SendAsync(op, connection, token).ConfigureAwait(false);
        }

        protected async Task<string> SaslList(IConnection connection, CancellationToken token)
        {
            using var op = new SaslList
            {
                Transcoder = Transcoder,
                Timeout = Timeout
            };
            return await SendAsync(op, connection, token).ConfigureAwait(false);
        }

        protected async Task<T> SendAsync<T>(IOperation<T> op, IConnection connection, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<IMemoryOwner<byte>>();
            op.Completed = s =>
            {
                //Status will be AuthenticationError if auth failed otherwise false
                if (s.Status == ResponseStatus.Success || s.Status == ResponseStatus.AuthenticationContinue)
                {
                    tcs.TrySetResult(s.ExtractData());
                }
                else
                {
                    tcs.SetException(
                        new AuthenticationFailureException(
                            $"Cannot authenticate the user. Reason: {s.Status}"));
                }

                return tcs.Task;
            };

            IDisposable? cancellationTokenRegistration = null;
            if (cancellationToken.CanBeCanceled)
            {
                // Not the default, so register the callback

                cancellationTokenRegistration = cancellationToken.Register(() =>
                {
                    tcs.TrySetCanceled(cancellationToken);
                });
            }

            try
            {
                await op.SendAsync(connection).ConfigureAwait(false);
                var bytes = await tcs.Task.ConfigureAwait(false);
                await op.ReadAsync(bytes).ConfigureAwait(false);

                return op.GetResultWithValue().Content;
            }
            finally
            {
                cancellationTokenRegistration?.Dispose();
            }
        }
    }
}
