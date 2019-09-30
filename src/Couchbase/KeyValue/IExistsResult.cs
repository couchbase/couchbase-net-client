namespace Couchbase.KeyValue
{
    public interface IExistsResult : IResult
    {
        bool Exists { get; }
    }
}
