
namespace Couchbase
{
    public interface IExistsResult : IResult
    {
        bool Exists { get; }
    }
}
