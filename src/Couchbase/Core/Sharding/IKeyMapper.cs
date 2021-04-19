
namespace Couchbase.Core.Sharding
{
    internal interface IKeyMapper
    {
        IMappedNode MapKey(string key);

        IMappedNode MapKey(string key, uint revision);

        ulong Rev { get; set; }
    }
}
