using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Core.Buckets
{
    /// <summary>
    /// A cluster node mapped to a given Key.
    /// </summary>
    internal class KetamaNode : IMappedNode
    {
        private readonly IServer _server;

        public KetamaNode(IServer server)
        {
            _server = server;
        }

        /// <summary>
        /// Gets the primary node for a key.
        /// </summary>
        /// <returns>An object implementing the <see cref="IServer"/> interface, 
        /// which is the node that a key is mapped to within a cluster.</returns>
        public IServer LocatePrimary()
        {
            return _server;
        }
    }
}
