using System.Runtime.InteropServices;
using System.Threading;
using Couchbase.Core.IO.Operations;

namespace Couchbase.Core.IO.Connections.Channels
{
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct ChannelQueueItem
    {
        public IOperation Operation { get; }
        public CancellationToken CancellationToken { get; }

        public ChannelQueueItem(IOperation operation, CancellationToken cancellationToken)
        {
            Operation = operation;
            CancellationToken = cancellationToken;
        }
    }
}
