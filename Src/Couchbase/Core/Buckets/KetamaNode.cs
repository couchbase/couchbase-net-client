using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Core.Buckets
{
    internal class KetamaNode : IMappedNode
    {
        private readonly IServer _server;

        public KetamaNode(IServer server)
        {
            _server = server;
        }

        public IServer LocatePrimary()
        {
            return _server;
        }
    }
}
