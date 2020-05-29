#nullable enable

namespace Couchbase.KeyValue
{
    /// <remarks>Volatile</remarks>
    public interface IScope
    {
        string Id { get; }

        string Name { get; }

        /// <summary>
        /// The bucket that owns this scope.
        /// </summary>
        IBucket Bucket { get; }

        ICouchbaseCollection this[string name] { get; }

        ICouchbaseCollection Collection(string collectionName);
    }
}
