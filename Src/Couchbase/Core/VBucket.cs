using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Core
{
    internal class VBucket : IVBucket
    {
        private readonly List<IServer> _cluster; 
        public VBucket(List<IServer> cluster, int index, int primary, int replica)
        {
            _cluster = cluster;
            Index = index;
            Primary = primary;
            Replica = replica;
        }

        public IServer LocatePrimary()
        {
            return _cluster[Primary];
        }

        public IServer LocateReplica()
        {
            return _cluster[Replica];
        }

        public List<IServer> Replicas
        {
            get { return _cluster.Skip(1).ToList(); }
        }

        public int Index { get; private set; }

        public int Primary { get; private set; }

        public int Replica { get; private set; }
    }
}
