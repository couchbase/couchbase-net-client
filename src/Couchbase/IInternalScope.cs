using System.Threading.Tasks;
using Couchbase.KeyValue;

namespace Couchbase
{
    /// <summary>
    /// Interface for any non-public methods or properties that are needed on a <see cref="IScope"/>.
    /// </summary>
    internal interface IInternalScope
    {
        /// <summary>
        /// Given a fully qualified name get the Identifier for a Collection.
        /// </summary>
        /// <param name="fullyQualifiedName">A string in the format {scopeName}.{collectionName}.</param>
        /// <returns>The identifier for a collection.</returns>
        Task<uint?> GetCidAsync(string fullyQualifiedName);
    }
}
