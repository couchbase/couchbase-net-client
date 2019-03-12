namespace Couchbase
{
    public interface ICounterResult : IMutationResult
    {
        ulong Content { get; }
    }
}
