using System.IO;

namespace Couchbase.IO
{
    internal class OperationAsyncState
    {
        public uint Opaque { get; set; }

        public MemoryStream Data { get; set; }

        public byte[] Buffer { get; set; }

        public int BytesReceived { get; set; }

        public int BodyLength { get; set; }
    }
}
