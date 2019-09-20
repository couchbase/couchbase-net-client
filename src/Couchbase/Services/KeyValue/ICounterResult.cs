namespace Couchbase.Services.KeyValue
{
    public interface ICounterResult : IMutationResult
    {
        ulong Content { get; }
    }
}
