using System.Threading.Tasks;

namespace Couchbase.Services.KeyValue
{
    public interface IScope
    {
        string Id { get; }

        string Name { get; }

        ICollection this[string name] { get; }

        Task<ICollection> CollectionAsync(string collectionName, CollectionOptions options);
    }
}
