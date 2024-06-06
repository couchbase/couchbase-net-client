#nullable enable
using System.Threading.Tasks;
using Couchbase.Core.Compatibility;

#pragma warning disable CS1591

namespace Couchbase.Integrated.Transactions.Internal.Test
{
    [InterfaceStability(Level.Volatile)]
    internal interface ICleanupTestHooks
    {
        public Task<int?> BeforeCommitDoc(string id);
        public Task<int?> BeforeDocGet(string id);
        public Task<int?> BeforeRemoveDocStagedForRemoval(string id);
        public Task<int?> BeforeRemoveDoc(string id);
        public Task<int?> BeforeAtrGet(string id);
        public Task<int?> BeforeAtrRemove(string id);
        public Task<int?> BeforeRemoveLinks(string id);
        public Task<int?> BeforeGetRecord(string clientUuid);
        public Task<int?> BeforeUpdateRecord(string clientUuid);
        public Task<int?> BeforeCreateRecord(string clientUuid);
        public Task<int?> BeforeRemoveClient(string clientUuid);
    }

    internal class DefaultCleanupTestHooks : ICleanupTestHooks
    {
        public static readonly ICleanupTestHooks Instance = new DefaultCleanupTestHooks();
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
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2024 Couchbase, Inc.
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





