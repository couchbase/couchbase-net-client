using System.Net;

namespace Couchbase.Core.Sharding
{
    /// <summary>
    /// A cluster node mapped to a given Key.
    /// </summary>
    internal class KetamaNode : IMappedNode
    {
        private readonly IPEndPoint _server;

        public KetamaNode(IPEndPoint server)
        {
            _server = server;
        }

        /// <summary>
        /// Gets the primary node for a key.
        /// </summary>
        /// <returns>An object implementing the <see cref="IServer"/> interface,
        /// which is the node that a key is mapped to within a cluster.</returns>
        public IPEndPoint LocatePrimary()
        {
            return _server;
        }

        public uint Rev { get; internal set; }
    }
}
