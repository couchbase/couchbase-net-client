namespace Couchbase.Services.KeyValue
{
    public interface IExistsResult : IResult
    {
        bool Exists { get; }
    }
}
