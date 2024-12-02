#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Retry;
using Couchbase.Integrated.Transactions.Components;
using Couchbase.Integrated.Transactions.DataModel;
using Couchbase.Integrated.Transactions.Support;
using Couchbase.KeyValue;
using Newtonsoft.Json.Linq;

namespace Couchbase.Integrated.Transactions.DataAccess
{
    internal class CleanerRepository
    {
        public KeySpace KeySpace { get; }
        private readonly ICluster _cluster;
        private static readonly int ExpiresSafetyMarginMillis = 20_000;
        private static readonly TimeSpan RemoveClientTimeout = TimeSpan.FromMilliseconds(500);
        private static readonly object PlaceholderEmptyJObject = JObject.Parse("{}");
        private ICouchbaseCollection? _collection = null;

        public CleanerRepository(KeySpace keySpaceToClean, ICluster cluster)
        {
            KeySpace = keySpaceToClean;
            _cluster = cluster;
        }

        public async Task<ICouchbaseCollection> GetCollection()
        {
            if (_collection is null)
            {
                var bkt = await _cluster.BucketAsync(KeySpace.Bucket).CAF();
                if (string.IsNullOrWhiteSpace(KeySpace.Scope))
                {
                    return bkt.DefaultCollection();
                }

                var scp = bkt.Scope(KeySpace.Scope);
                if (string.IsNullOrWhiteSpace(KeySpace.Collection))
                {
                    return scp.Collection("_default");
                }

                var col = scp.Collection(KeySpace.Collection);
                _collection = col;
            }

            return _collection;
        }

        public async Task CreatePlaceholderClientRecord(ulong? cas = null)
        {
            var opts = new MutateInOptions()
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

            var col = await GetCollection().CAF();
            _ = await col.MutateInAsync(ClientRecordsIndex.CLIENT_RECORD_DOC_ID, specs, opts).CAF();
        }

        public async Task<(ClientRecordsIndex? clientRecord, ParsedHLC? parsedHlc, ulong? cas)> GetClientRecord(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var opts = new LookupInOptions().Transcoder(Transactions.MetadataTranscoder).CancellationToken(token);
            opts.PreferReturn = true;
            var specs = new LookupInSpec[]
            {
                LookupInSpec.Get(ClientRecordsIndex.FIELD_RECORDS, isXattr: true),
                LookupInSpec.Get(ClientRecordsIndex.VBUCKET_HLC, isXattr: true)
            };

            var col = await GetCollection().CAF();
            var lookupInResult = await col.LookupInAsync(ClientRecordsIndex.CLIENT_RECORD_DOC_ID, specs, opts).CAF();
            if (lookupInResult is { Cas: > 0 })
            {
                var parsedRecord = lookupInResult.ContentAs<ClientRecordsIndex>(0);
                var parsedHlc = lookupInResult.ContentAs<ParsedHLC>(1);
                return (parsedRecord, parsedHlc, lookupInResult.Cas);
            }

            return (null, null, 0);
        }

        private static readonly LookupInSpec[] LookupAttemptsSpec = new LookupInSpec[]
        {
            LookupInSpec.Get(TransactionFields.AtrFieldAttempts, isXattr: true),
            LookupInSpec.Get(ClientRecordsIndex.VBUCKET_HLC, isXattr: true)
        };

        private static readonly LookupInOptions LookupAttemptsOptions = new LookupInOptions() { PreferReturn = true }
            .Transcoder(Transactions.MetadataTranscoder).RetryStrategy(new FailFastRetryStrategy());

        public async Task<(Dictionary<string, AtrEntry>? attempts, ParsedHLC? parsedHlc, double[] timingInfo)> LookupAttempts(string atrId)
        {
            var sw = Stopwatch.StartNew();
            var elapsed1 = sw.Elapsed.TotalMilliseconds;
            var col = await GetCollection().CAF();
            var elapsed2 = sw.Elapsed.TotalMilliseconds;
            var lookupInResult = await col.LookupInAsync(atrId, LookupAttemptsSpec, LookupAttemptsOptions).CAF();
            var elapsed3 = sw.Elapsed.TotalMilliseconds;
            if (lookupInResult is not { Cas: > 0 })
            {
                return (null, null, [elapsed1, elapsed2, elapsed3]);
            }
            var attempts = lookupInResult.ContentAs<Dictionary<string, AtrEntry>>(0);
            var parsedHlc = lookupInResult.ContentAs<ParsedHLC>(1);
            sw.Stop();
            var elapsed4 = sw.Elapsed.TotalMilliseconds;
            return (attempts, parsedHlc, [elapsed1, elapsed2, elapsed3, elapsed4]);
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

            var col = await GetCollection().CAF();
            _ = await col.MutateInAsync(ClientRecordsIndex.CLIENT_RECORD_DOC_ID, specs, opts).CAF();
        }

        public async Task UpdateClientRecord(string clientUuid, TimeSpan cleanupWindow, int numAtrs, IReadOnlyList<string> expiredClientIds)
        {
            var prefix = ClientRecordEntry.PathForEntry(clientUuid);
            var opts = new MutateInOptions().Transcoder(Transactions.MetadataTranscoder);
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

            var col = await GetCollection().CAF();
            _ = await col.MutateInAsync(ClientRecordsIndex.CLIENT_RECORD_DOC_ID, specs, opts).CAF();
        }
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







