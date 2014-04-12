using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.Authentication;

namespace Couchbase.Authentication.SASL
{
    internal class AuthAsyncState
    {
        public IConnection Connection { get; set; }

        public byte[] Buff = new byte[512];

        public MemoryStream Data = new MemoryStream();

        public SaslListMechanism ListMechanism { get; set; }
    }
}
