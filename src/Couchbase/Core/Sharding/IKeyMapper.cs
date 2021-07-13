
namespace Couchbase.Core.Sharding
{
    internal interface IKeyMapper
    {
        IMappedNode MapKey(string key);

        IMappedNode MapKey(string key, bool notMyVBucket);

        ulong Rev { get; set; }
    }
}
