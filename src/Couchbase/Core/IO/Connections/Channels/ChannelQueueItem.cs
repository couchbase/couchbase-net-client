using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations;

#nullable enable

namespace Couchbase.Core.IO.Connections.Channels
{
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct ChannelQueueItem
    {
        public IOperation Operation { get; }
        public CancellationToken CancellationToken { get; }

        // Capture the execution context when we're queueing an item so that operations related to
        // it run on the same context. This is important to ensure correct linkage to parent activity
        // traces when not all traces are sampled.
        public ExecutionContext? CapturedContext { get; }

        public ChannelQueueItem(IOperation operation, CancellationToken cancellationToken, bool captureContext = true)
        {
            Operation = operation;
            CancellationToken = cancellationToken;

            if (captureContext)
            {
                CapturedContext = ExecutionContext.Capture();
            }
        }

        public Task SendAsync(IConnection connection)
        {
            if (CapturedContext is null)
            {
                // ReSharper disable once MethodSupportsCancellation
                // We don't want to forward the cancellation token for this connection, as that isn't the
                // same as the cancellation token for the operation. If this connection is being shutdown
                // while the operation is being sent, we'll let it finish sending.
                return Operation.SendAsync(connection);
            }
            else
            {
                // There is a captured ExecutionContext, execute the operation on the captured context.
                // The state management is hand-coded instead of using a closure. This avoids allocating
                // both a delegate and a closure for each call, instead only allocating the state object.

                var sendState = new SendState(Operation, connection);
                ExecutionContext.Run(CapturedContext, SendCallback, sendState);

                return sendState.Result!;
            }
        }

        private static void SendCallback(object? state)
        {
            var sendState = (SendState) state!;

            sendState.Result = sendState.Operation.SendAsync(sendState.Connection);
        }

        private sealed class SendState
        {
            public readonly IOperation Operation;
            public readonly IConnection Connection;
            public Task? Result;

            public SendState(IOperation operation, IConnection connection)
            {
                Operation = operation;
                Connection = connection;
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
