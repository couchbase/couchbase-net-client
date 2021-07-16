using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations.SubDocument;

#nullable enable

namespace Couchbase.KeyValue
{
    public interface ICouchbaseCollection
    {
        uint? Cid { get; }

        string Name { get; }

        /// <summary>
        /// Scope which owns this collection.
        /// </summary>
        IScope Scope { get; }

        IBinaryCollection Binary { get; }

        #region Basic

        Task<IGetResult> GetAsync(string id, GetOptions? options = null);

        Task<IExistsResult> ExistsAsync(string id, ExistsOptions? options = null);

        Task<IMutationResult> UpsertAsync<T>(string id, T content, UpsertOptions? options = null);

        Task<IMutationResult> InsertAsync<T>(string id, T content, InsertOptions? options = null);

        Task<IMutationResult> ReplaceAsync<T>(string id, T content, ReplaceOptions? options = null);

        Task RemoveAsync(string id, RemoveOptions? options = null);

        [Obsolete("Use overload that does not have a Type parameter T.")]
        Task UnlockAsync<T>(string id, ulong cas, UnlockOptions? options = null);

        Task UnlockAsync(string id, ulong cas, UnlockOptions? options = null);

        Task TouchAsync(string id, TimeSpan expiry, TouchOptions? options = null);

        Task<IGetResult> GetAndTouchAsync(string id, TimeSpan expiry, GetAndTouchOptions? options = null);

        Task<IGetResult> GetAndLockAsync(string id, TimeSpan expiry, GetAndLockOptions? options = null);

        Task<IGetReplicaResult> GetAnyReplicaAsync(string id, GetAnyReplicaOptions? options = null);

        IEnumerable<Task<IGetReplicaResult>> GetAllReplicasAsync(string id, GetAllReplicasOptions? options = null);

        #endregion

        #region Subdoc

        Task<ILookupInResult> LookupInAsync(string id, IEnumerable<LookupInSpec> specs, LookupInOptions? options = null);

        Task<IMutateInResult> MutateInAsync(string id, IEnumerable<MutateInSpec> specs, MutateInOptions? options = null);

        #endregion
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
