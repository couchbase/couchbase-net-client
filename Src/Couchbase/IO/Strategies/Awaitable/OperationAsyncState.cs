using System.IO;

namespace Couchbase.IO.Strategies.Awaitable
{
    internal sealed class OperationAsyncState
    {
        public const int HeaderLength = 24;

        public int TotalLength { get { return HeaderLength + BodyLength; } }

        public int OperationId { get; set; }

        public IConnection Connection { get; set; }

        public byte[] Buff = new byte[512];

        public byte[] Buffer { get; set; }

        public int BodyLength { get; set; }

        public int ExtrasLength { get; set; }

        public int KeyLength { get; set; }

        public MemoryStream Data = new MemoryStream();

        public int BytesSent { get; set; }

        public bool HasBegun { get; set; }
    }
}
