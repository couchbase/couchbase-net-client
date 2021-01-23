using Couchbase.Core.Compatibility;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Result of a sub document LookupIn operation.
    /// </summary>
    public interface ILookupInResult : IResult
    {
        bool Exists(int index);

        bool IsDeleted { get; }

        T ContentAs<T>(int index);

        /// <summary>
        /// Returns the index of a particular path.
        /// </summary>
        /// <param name="path">Path to find.</param>
        /// <returns>The index of the path, or -1 if not found.</returns>
        [InterfaceStability(Level.Volatile)]
        int IndexOf(string path);
    }
}
