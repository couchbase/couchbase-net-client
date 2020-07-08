#nullable enable

namespace Couchbase.KeyValue
{
    public interface IMutateInResult : IMutationResult
    {
        /// <summary>
        /// Gets the content of a mutation as the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the content</typeparam>
        /// <param name="index">The spec index.</param>
        /// <returns>The content, if the operation was an Increment or Decrement, otherwise <c>default(T)</c>.</returns>
        T ContentAs<T>(int index);
    }
}
