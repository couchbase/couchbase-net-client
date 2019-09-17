namespace Couchbase
{
    public interface IGetReplicaResult : IGetResult
    {
        bool IsMaster { get; }
    }
}
