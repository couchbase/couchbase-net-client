using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.DataStructures
{
    public interface IPersistentSet<TValue> : ISet<TValue>
    {
        Task<bool> AddAsync(TValue item);

        Task ExceptWithAsync(IEnumerable<TValue> other);

        Task IntersectWithAsync(IEnumerable<TValue> other);

        Task<bool> IsProperSubsetOfAsync(IEnumerable<TValue> other);

        Task<bool> IsProperSupersetOfAsync(IEnumerable<TValue> other);

        Task<bool> IsSubsetOfAsync(IEnumerable<TValue> other);

        Task<bool> IsSupersetOfAsync(IEnumerable<TValue> other);

        Task<bool> OverlapsAsync(IEnumerable<TValue> other);

        Task<bool> SetEqualsAsync(IEnumerable<TValue> other);

        Task SymmetricExceptWithAsync(IEnumerable<TValue> other);

        Task UnionWithAsync(IEnumerable<TValue> other);

        Task ClearAsync();

        Task<bool> ContainsAsync(TValue item);

        Task CopyToAsync(TValue[] array, int arrayIndex);

        Task<bool> RemoveAsync(TValue item);

        Task<int> CountAsync { get; }
    }

}
