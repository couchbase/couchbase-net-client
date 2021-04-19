using System.Net;

namespace Couchbase.Core
{
    public interface IMappedNode
    {
        IPEndPoint LocatePrimary();

        ulong Rev { get; }
    }
}
