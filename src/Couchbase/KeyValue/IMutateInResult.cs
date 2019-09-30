namespace Couchbase.KeyValue
{
    public interface IMutateInResult : IMutationResult
    {
        T ContentAs<T>(int index);
    }
}
