using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations;

#nullable enable

namespace Couchbase.Core.IO.Connections.DataFlow
{
    internal class SendOperationRequest
    {
        private readonly TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();

        public IOperation Operation { get; }
        public CancellationToken CancellationToken { get; }
        public Task CompletionTask => _tcs.Task;

        public SendOperationRequest(IOperation operation, CancellationToken cancellationToken)
        {
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            CancellationToken = cancellationToken;
        }

        public async Task SendAsync(IConnection connection)
        {
            try
            {
                IDisposable? tokenRegistration = null;
                if (CancellationToken.CanBeCanceled)
                {
                    CancellationToken.ThrowIfCancellationRequested();

                    tokenRegistration = CancellationToken.Register(
                        () => _tcs.TrySetCanceled(CancellationToken));
                }

                try
                {
                    await Operation.SendAsync(connection).ConfigureAwait(false);

                    _tcs.TrySetResult(true);
                }
                finally
                {
                    tokenRegistration?.Dispose();
                }
            }
            catch (OperationCanceledException ex)
            {
                _tcs.TrySetCanceled(ex.CancellationToken);
            }
            catch (Exception ex)
            {
                _tcs.TrySetException(ex);
            }
        }
    }
}
