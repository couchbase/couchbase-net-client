using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Transactions.Cleanup.LostTransactions;

namespace Couchbase.Transactions.Internal.Test
{
    public interface ICleanupTestHooks
    {
        public Task<int?> BeforeCommitDoc(string id) => Task.FromResult((int?)1);
        public Task<int?> BeforeDocGet(string id) => Task.FromResult((int?)1);
        public Task<int?> BeforeRemoveDocStagedForRemoval(string id) => Task.FromResult((int?)1);
        public Task<int?> BeforeRemoveDoc(string id) => Task.FromResult((int?)1);
        public Task<int?> BeforeAtrGet(string id) => Task.FromResult((int?)1);
        public Task<int?> BeforeAtrRemove(string id) => Task.FromResult((int?)1);
        public Task<int?> BeforeRemoveLinks(string id) => Task.FromResult((int?)1);
        public Task<int?> BeforeGetRecord(string clientUuid) => Task.FromResult<int?>(1);
        public Task<int?> BeforeUpdateRecord(string clientUuid) => Task.FromResult<int?>(1);
        public Task<int?> BeforeCreateRecord(string clientUuid) => Task.FromResult<int?>(1);
        public Task<int?> BeforeRemoveClient(string clientUuid) => Task.FromResult<int?>(1);
    }

    internal class DefaultCleanupTestHooks : ICleanupTestHooks
    {
        public static readonly ICleanupTestHooks Instance = new DefaultCleanupTestHooks();
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
