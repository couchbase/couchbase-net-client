using System.IO;
using Couchbase.IO.Operations;

namespace Couchbase.IO.Strategies.Awaitable
{
    /// <summary>
    /// Maintains state while an asynchronous operation is in progress.
    /// </summary>
    internal sealed class OperationAsyncState
    {
        /// <summary>
        /// A unique identifier for the operation.
        /// </summary>
        public int OperationId { get; set; }

        /// <summary>
        /// The <see cref="IConnection"/> object used during the asynchronous operation.
        /// </summary>
        public IConnection Connection { get; set; }

        /// <summary>
        /// A read/write buffer that defaults to 512bytes
        /// </summary>
        public byte[] Buffer = new byte[512];

        /// <summary>
        /// The temporary stream for holding data for the current operation.
        /// </summary>
        public MemoryStream Data = new MemoryStream();

        /// <summary>
        /// The <see cref="OperationHeader"/> of the current operation.
        /// </summary>
        public OperationHeader Header;

        /// <summary>
        /// The <see cref="OperationBody"/> of the current operation.
        /// </summary>
        public OperationBody Body;

        /// <summary>
        /// A current count of the bytes recieved for the current operation.
        /// </summary>
        public int BytesReceived { get; set; } 

        /// <summary>
        /// Sets all values back to their defaults, so this object can be reused.
        /// </summary>
        public void Reset()
        {
            if(Data != null)
            {
                Data.Dispose();
            }
            Data = new MemoryStream();
            BytesReceived = 0;
            Header = new OperationHeader();
            Body = new OperationBody();
        }
    }
}
