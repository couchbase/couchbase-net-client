namespace Couchbase.Services.KeyValue
{
    public interface IMutateInResult : IMutationResult
    {
        T ContentAs<T>(int index);
    }
}
