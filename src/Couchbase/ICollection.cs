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

        #region Get

        Task<IGetResult> Get(string id);

        Task<IGetResult> Get(string id, Action<GetOptions> configureOptions);

        Task<IGetResult> Get(string id, GetOptions options);

        #endregion

        #region Exists

        Task<IExistsResult> Exists(string id);

        Task<IExistsResult> Exists(string id, Action<ExistsOptions> configureOptions);

        Task<IExistsResult> Exists(string id, ExistsOptions options);

        #endregion

        #region Upsert

        Task<IMutationResult> Upsert<T>(string id, T content);

        Task<IMutationResult> Upsert<T>(string id, T content, Action<UpsertOptions> configureOptions);

        Task<IMutationResult> Upsert<T>(string id, T content, UpsertOptions options);

        #endregion

        #region Insert

        Task<IMutationResult> Insert<T>(string id, T content);

        Task<IMutationResult> Insert<T>(string id, T content, Action<InsertOptions> optionsAction);

        Task<IMutationResult> Insert<T>(string id, T content, InsertOptions options);

        #endregion

        #region Replace

        Task<IMutationResult> Replace<T>(string id, T content);

        Task<IMutationResult> Replace<T>(string id, T content, Action<ReplaceOptions> configureOptions);

        Task<IMutationResult> Replace<T>(string id, T content, ReplaceOptions options);

        #endregion

        #region Remove

        Task Remove(string id);

        Task Remove(string id, Action<RemoveOptions> configureOptions);

        Task Remove(string id, RemoveOptions options);

        #endregion

        #region Unlock

        Task Unlock<T>(string id);

        Task Unlock<T>(string id, Action<UnlockOptions> configureOptions);

        Task Unlock<T>(string id, UnlockOptions options);

        #endregion

        #region Touch

        Task Touch(string id, TimeSpan expiration);

        Task Touch(string id, TimeSpan expiration, Action<TouchOptions> configureOptions);

        Task Touch(string id, TimeSpan expiration, TouchOptions options);

        #endregion

        #region GetAndTouch

        Task<IGetResult> GetAndTouch(string id, TimeSpan expiration);

        Task<IGetResult> GetAndTouch(string id, TimeSpan expiration, Action<GetAndTouchOptions> configureOptions);

        Task<IGetResult> GetAndTouch(string id, TimeSpan expiration, GetAndTouchOptions options);

        #endregion

        #region GetAndLock

        Task<IGetResult> GetAndLock(string id, TimeSpan expiration);

        Task<IGetResult> GetAndLock(string id, TimeSpan expiration, Action<GetAndLockOptions> configureOptions);

        Task<IGetResult> GetAndLock(string id, TimeSpan expiration, GetAndLockOptions options);

        #endregion

        #region LookupIn

        Task<ILookupInResult> LookupIn(string id, Action<LookupInSpecBuilder> configureBuilder);

        Task<ILookupInResult> LookupIn(string id, Action<LookupInSpecBuilder> configureBuilder, Action<LookupInOptions> configureOptions);

        Task<ILookupInResult> LookupIn(string id, Action<LookupInSpecBuilder> configureBuilder, LookupInOptions options);

        Task<ILookupInResult> LookupIn(string id, IEnumerable<OperationSpec> specs);

        Task<ILookupInResult> LookupIn(string id, IEnumerable<OperationSpec> specs, Action<LookupInOptions> configureOptions);

        Task<ILookupInResult> LookupIn(string id, IEnumerable<OperationSpec> specs, LookupInOptions options);

        #endregion

        #region MutateIn

        Task<IMutationResult> MutateIn(string id, Action<MutateInSpecBuilder> configureBuilder);

        Task<IMutationResult> MutateIn(string id, Action<MutateInSpecBuilder> configureBuilder, Action<MutateInOptions> configureOptions);

        Task<IMutationResult> MutateIn(string id, Action<MutateInSpecBuilder> configureBuilder, MutateInOptions options);

        Task<IMutationResult> MutateIn(string id, IEnumerable<OperationSpec> specs);

        Task<IMutationResult> MutateIn(string id, IEnumerable<OperationSpec> specs, Action<MutateInOptions> configureOptions);

        Task<IMutationResult> MutateIn(string id, IEnumerable<OperationSpec> specs, MutateInOptions options);

        #endregion
    }
}
