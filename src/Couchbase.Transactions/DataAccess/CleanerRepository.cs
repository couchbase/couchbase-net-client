using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Couchbase.Transactions.Components;
using Couchbase.Transactions.DataModel;
using Couchbase.Transactions.Support;
using Newtonsoft.Json.Linq;

namespace Couchbase.Transactions.DataAccess
{
    internal class CleanerRepository : ICleanerRepository
    {
        private static readonly int ExpiresSafetyMarginMillis = 20_000;
        private static readonly TimeSpan RemoveClientTimeout = TimeSpan.FromMilliseconds(500);
        private static readonly object PlaceholderEmptyJObject = JObject.Parse("{}");
        public ICouchbaseCollection Collection { get; }
        private readonly TimeSpan? _keyValueTimeout;

        public CleanerRepository(ICouchbaseCollection collection, TimeSpan? keyValueTimeout)
        {
            Collection = collection;
            _keyValueTimeout = keyValueTimeout;
        }

        public async Task CreatePlaceholderClientRecord(ulong? cas = null)
        {
            var opts = new MutateInOptions()
                .Timeout(_keyValueTimeout)
                .StoreSemantics(StoreSemantics.Insert)
                .Transcoder(Transactions.MetadataTranscoder);

            if (cas != null)
            {
                // NOTE: To handle corrupt case where placeholder "_txn:client-record" was there, but 'records' XATTR was not.
                //       This needs to be addressed in the RFC, as a misbehaving client will cause all other clients to never work.
                opts.Cas(0).StoreSemantics(StoreSemantics.Upsert);
            }

            var specs = new MutateInSpec[]
            {
                MutateInSpec.Insert(ClientRecordsIndex.FIELD_CLIENTS_FULL, PlaceholderEmptyJObject, isXattr: true),
                MutateInSpec.SetDoc(new byte?[] { null }), // ExtBinaryMetadata
            };

            _ = await Collection.MutateInAsync(ClientRecordsIndex.CLIENT_RECORD_DOC_ID, specs, opts).CAF();
        }

        public async Task<(ClientRecordsIndex? clientRecord, ParsedHLC? parsedHlc, ulong? cas)> GetClientRecord()
        {
            var opts = new LookupInOptions().Timeout(_keyValueTimeout).Transcoder(Transactions.MetadataTranscoder);
            var specs = new LookupInSpec[]
            {
                LookupInSpec.Get(ClientRecordsIndex.FIELD_RECORDS, isXattr: true),
                LookupInSpec.Get(ClientRecordsIndex.VBUCKET_HLC, isXattr: true)
            };

            var lookupInResult = await Collection.LookupInAsync(ClientRecordsIndex.CLIENT_RECORD_DOC_ID, specs, opts).CAF();
            var parsedRecord = lookupInResult.ContentAs<ClientRecordsIndex>(0);
            var parsedHlc = lookupInResult.ContentAs<ParsedHLC>(1);
            return (parsedRecord, parsedHlc, lookupInResult.Cas);
        }

        public async Task<(Dictionary<string, AtrEntry>? attempts, ParsedHLC? parsedHlc)> LookupAttempts(string atrId)
        {
            var opts = new LookupInOptions().Timeout(_keyValueTimeout).Transcoder(Transactions.MetadataTranscoder);
            var specs = new LookupInSpec[]
            {
                LookupInSpec.Get(TransactionFields.AtrFieldAttempts, isXattr: true),
                LookupInSpec.Get(ClientRecordsIndex.VBUCKET_HLC, isXattr: true)
            };

            var lookupInResult = await Collection.LookupInAsync(atrId, specs, opts).CAF();
            var attempts = lookupInResult.ContentAs<Dictionary<string, AtrEntry>>(0);
            var parsedHlc = lookupInResult.ContentAs<ParsedHLC>(1);
            return (attempts, parsedHlc);
        }

        public async Task RemoveClient(string clientUuid, DurabilityLevel durability = DurabilityLevel.None)
        {
            var opts = new MutateInOptions()
                .Timeout(RemoveClientTimeout)
                .Durability(DurabilityLevel.None)
                .Transcoder(Transactions.MetadataTranscoder);

            var specs = new MutateInSpec[]
            {
                MutateInSpec.Remove(ClientRecordEntry.PathForEntry(clientUuid), isXattr: true),
            };

            _ = await Collection.MutateInAsync(ClientRecordsIndex.CLIENT_RECORD_DOC_ID, specs, opts).CAF();
        }

        public async Task UpdateClientRecord(string clientUuid, TimeSpan cleanupWindow, int numAtrs, IReadOnlyList<string> expiredClientIds)
        {
            var prefix = ClientRecordEntry.PathForEntry(clientUuid);
            var opts = new MutateInOptions().Timeout(_keyValueTimeout).Transcoder(Transactions.MetadataTranscoder);
            var specs = new List<MutateInSpec>
            {
                MutateInSpec.Upsert(ClientRecordEntry.PathForHeartbeat(clientUuid), MutationMacro.Cas, createPath: true),
                MutateInSpec.Upsert(ClientRecordEntry.PathForExpires(clientUuid), (int)cleanupWindow.TotalMilliseconds + ExpiresSafetyMarginMillis, isXattr: true),
                MutateInSpec.Upsert(ClientRecordEntry.PathForNumAtrs(clientUuid), numAtrs, isXattr: true),
                MutateInSpec.SetDoc(new byte?[] { null }), // ExtBinaryMetadata
            };

            var remainingSpecLimit = 16 - specs.Count;
            foreach (var clientId in expiredClientIds.Take(remainingSpecLimit))
            {
                var spec = MutateInSpec.Remove($"{ClientRecordsIndex.FIELD_CLIENTS_FULL}.{clientId}", isXattr: true);
                specs.Add(spec);
            }

            _ = await Collection.MutateInAsync(ClientRecordsIndex.CLIENT_RECORD_DOC_ID, specs, opts).CAF();
        }
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
