using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations.SubDocument;

namespace Couchbase
{
    public interface ICollection
    {
        uint? Cid { get; }

        string Name { get; }

        IBinaryCollection Binary { get; }

        #region Basic

        Task<IGetResult> GetAsync(string id, GetOptions options);

        Task<IExistsResult> ExistsAsync(string id, ExistsOptions options);

        Task<IMutationResult> UpsertAsync<T>(string id, T content, UpsertOptions options);

        Task<IMutationResult> InsertAsync<T>(string id, T content, InsertOptions options);

        Task<IMutationResult> ReplaceAsync<T>(string id, T content, ReplaceOptions options);

        Task RemoveAsync(string id, RemoveOptions options);

        Task UnlockAsync<T>(string id, UnlockOptions options);

        Task TouchAsync(string id, TimeSpan expiry, TouchOptions options);

        Task<IGetResult> GetAndTouchAsync(string id, TimeSpan expiry, GetAndTouchOptions options);

        Task<IGetResult> GetAndLockAsync(string id, TimeSpan expiry, GetAndLockOptions options);

        Task<IGetReplicaResult> GetAnyReplicaAsync(string id, GetAnyReplicaOptions options);

        IEnumerable<Task<IGetReplicaResult>> GetAllReplicasAsync(string id, GetAllReplicasOptions options);

        #endregion

        #region Subdoc

        Task<ILookupInResult> LookupInAsync(string id, IEnumerable<OperationSpec> specs, LookupInOptions options);

        Task<IMutationResult> MutateInAsync(string id, IEnumerable<OperationSpec> specs, MutateInOptions options);

        #endregion
    }
}
