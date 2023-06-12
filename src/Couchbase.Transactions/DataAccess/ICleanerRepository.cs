using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Couchbase.Transactions.Components;
using Couchbase.Transactions.DataModel;

namespace Couchbase.Transactions.DataAccess
{
    internal interface ICleanerRepository
    {
        string BucketName => Collection.Scope.Bucket.Name;
        string ScopeName => Collection.Scope.Name;
        string CollectionName => Collection.Name;
        ICouchbaseCollection Collection { get; }
        Task<(ClientRecordsIndex? clientRecord, ParsedHLC? parsedHlc, ulong? cas)> GetClientRecord();
        Task CreatePlaceholderClientRecord(ulong? cas = null);
        Task RemoveClient(string clientUuid, DurabilityLevel durability = DurabilityLevel.None);
        Task UpdateClientRecord(string clientUuid, TimeSpan cleanupWindow, int numAtrs, IReadOnlyList<string> expiredClientIds);

        Task<(Dictionary<string, AtrEntry>? attempts, ParsedHLC? parsedHlc)> LookupAttempts(string atrId);
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
