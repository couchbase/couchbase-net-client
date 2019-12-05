using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations.SubDocument;

namespace Couchbase.KeyValue
{
    public interface ICollection
    {
        uint? Cid { get; }

        string Name { get; }

        IBinaryCollection Binary { get; }

        #region Basic

        Task<IGetResult> GetAsync(string id, GetOptions options = null);

        Task<IExistsResult> ExistsAsync(string id, ExistsOptions options = null);

        Task<IMutationResult> UpsertAsync<T>(string id, T content, UpsertOptions options = null);

        Task<IMutationResult> InsertAsync<T>(string id, T content, InsertOptions options = null);

        Task<IMutationResult> ReplaceAsync<T>(string id, T content, ReplaceOptions options = null);

        Task RemoveAsync(string id, RemoveOptions options = null);

        Task UnlockAsync<T>(string id, UnlockOptions options = null);

        Task TouchAsync(string id, TimeSpan expiry, TouchOptions options = null);

        Task<IGetResult> GetAndTouchAsync(string id, TimeSpan expiry, GetAndTouchOptions options = null);

        Task<IGetResult> GetAndLockAsync(string id, TimeSpan expiry, GetAndLockOptions options = null);

        Task<IGetReplicaResult> GetAnyReplicaAsync(string id, GetAnyReplicaOptions options = null);

        IEnumerable<Task<IGetReplicaResult>> GetAllReplicasAsync(string id, GetAllReplicasOptions options = null);

        #endregion

        #region Subdoc

        Task<ILookupInResult> LookupInAsync(string id, IEnumerable<OperationSpec> specs, LookupInOptions options = null);

        Task<IMutationResult> MutateInAsync(string id, IEnumerable<OperationSpec> specs, MutateInOptions options = null);

        #endregion
    }
}
