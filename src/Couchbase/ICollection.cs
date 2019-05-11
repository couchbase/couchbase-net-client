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

        Task<IGetResult> Get(string id, GetOptions options);

        Task<IExistsResult> Exists(string id, ExistsOptions options);

        Task<IMutationResult> Upsert<T>(string id, T content, UpsertOptions options);

        Task<IMutationResult> Insert<T>(string id, T content, InsertOptions options);

        Task<IMutationResult> Replace<T>(string id, T content, ReplaceOptions options);

        Task Remove(string id, RemoveOptions options);

        Task Unlock<T>(string id, UnlockOptions options);

        Task Touch(string id, TimeSpan expiration, TouchOptions options);

        Task<IGetResult> GetAndTouch(string id, TimeSpan expiration, GetAndTouchOptions options);

        Task<IGetResult> GetAndLock(string id, TimeSpan expiration, GetAndLockOptions options);

        #endregion

        #region Subdoc

        Task<ILookupInResult> LookupIn(string id, IEnumerable<OperationSpec> specs, LookupInOptions options);

        Task<IMutationResult> MutateIn(string id, IEnumerable<OperationSpec> specs, MutateInOptions options);

        #endregion
    }
}
