
namespace Couchbase
{
    public interface IMutateInResult : IMutationResult
    {
        T ContentAs<T>(int index);
    }
}
