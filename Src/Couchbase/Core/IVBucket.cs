using System.Collections.Generic;

namespace Couchbase.Core
{
    internal interface IVBucket : IMappedNode
    {
        IServer LocateReplica();

        List<IServer> Replicas { get; }

        int Index { get; }

        int Primary { get; }

        int Replica { get; }
    }
}
