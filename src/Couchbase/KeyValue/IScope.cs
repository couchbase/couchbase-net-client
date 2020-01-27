#nullable enable

namespace Couchbase.KeyValue
{
    /// <remarks>Volatile</remarks>
    public interface IScope
    {
        string Id { get; }

        string Name { get; }

        ICollection this[string name] { get; }

        ICollection Collection(string collectionName);
    }
}
