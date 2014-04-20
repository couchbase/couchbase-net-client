using System.IO;
using Couchbase.IO.Operations;

namespace Couchbase.IO.Strategies.Awaitable
{
    internal sealed class OperationAsyncState
    {
        public int OperationId { get; set; }

        public IConnection Connection { get; set; }

        public byte[] Buffer = new byte[512];

        public MemoryStream Data = new MemoryStream();

        public OperationHeader Header;

        public OperationBody Body;

        public int Offset;

        public int Length;

        public int BytesReceived { get; set; } 

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
            Offset = 0;
            Length = 0;
        }
    }
}
