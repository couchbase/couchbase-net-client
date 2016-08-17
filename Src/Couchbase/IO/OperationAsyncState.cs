using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.IO
{
    public class OperationAsyncState
    {
        public uint Opaque { get; set; }

        public MemoryStream Data { get; set; }

        public byte[] Buffer { get; set; }

        public int BytesReceived { get; set; }

        public int BodyLength { get; set; }
    }
}
