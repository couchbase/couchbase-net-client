#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Couchbase.Client.Transactions.Components;
using Couchbase.Client.Transactions.DataModel;
using Couchbase.Client.Transactions.LogUtil;
using Couchbase.Client.Transactions.Support;
using Couchbase.Core.IO.Operations;
using Couchbase.Protostellar.KV.V1;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using DurabilityLevel = Couchbase.KeyValue.DurabilityLevel;

namespace Couchbase.Client.Transactions.DataAccess
{
    internal class AtrRepository : IAtrRepository
    {
        private readonly string _attemptId;
        private readonly TransactionContext _overallContext;
        private readonly string _prefixedAtrFieldDocsInserted;
        private readonly string _prefixedAtrFieldDocsRemoved;
        private readonly string _prefixedAtrFieldDocsReplaced;
        private readonly string _prefixedAtrFieldExpiresAfterMsecs;
        private readonly string _prefixedAtrFieldsPendingSentinel;
        private readonly string _prefixedAtrFieldStartCommit;
        private readonly string _prefixedAtrFieldStartTimestamp;
        private readonly string _prefixedAtrFieldStatus;
        private readonly string _prefixedAtrFieldTimestampComplete;
        private readonly string _prefixedAtrFieldTimestampRollbackComplete;
        private readonly string _prefixedAtrFieldTimestampRollbackStart;
        private readonly string _prefixedAtrFieldTransactionId;
        private readonly string _prefixedAtrFieldDurability;
        private readonly DurabilityLevel? _atrDurability;
        private readonly ILogger _logger;


        private readonly string _atrRoot;


        public AtrRepository(string attemptId, TransactionContext overallContext, ICouchbaseCollection atrCollection, string atrId, DurabilityLevel? atrDurability, ILoggerFactory loggerFactory, string? testHookAtrId = null)
        : base(atrCollection, testHookAtrId ?? atrId) {
            // Ugly test hook handling.
            _atrRoot = $"{TransactionFields.AtrFieldAttempts}.{attemptId}";
            _attemptId = attemptId;
            _overallContext = overallContext;
            _prefixedAtrFieldDocsInserted = $"{_atrRoot}.{TransactionFields.AtrFieldDocsInserted}";
            _prefixedAtrFieldDocsRemoved = $"{_atrRoot}.{TransactionFields.AtrFieldDocsRemoved}";
            _prefixedAtrFieldDocsReplaced = $"{_atrRoot}.{TransactionFields.AtrFieldDocsReplaced}";
            _prefixedAtrFieldExpiresAfterMsecs = $"{_atrRoot}.{TransactionFields.AtrFieldExpiresAfterMsecs}";
            _prefixedAtrFieldsPendingSentinel = $"{_atrRoot}.{TransactionFields.AtrFieldPendingSentinel}";
            _prefixedAtrFieldStartCommit = $"{_atrRoot}.{TransactionFields.AtrFieldStartCommit}";
            _prefixedAtrFieldStartTimestamp = $"{_atrRoot}.{TransactionFields.AtrFieldStartTimestamp}";
            _prefixedAtrFieldStatus = $"{_atrRoot}.{TransactionFields.AtrFieldStatus}";
            _prefixedAtrFieldTimestampComplete = $"{_atrRoot}.{TransactionFields.AtrFieldTimestampComplete}";
            _prefixedAtrFieldTimestampRollbackComplete = $"{_atrRoot}.{TransactionFields.AtrFieldTimestampRollbackComplete}";
            _prefixedAtrFieldTimestampRollbackStart = $"{_atrRoot}.{TransactionFields.AtrFieldTimestampRollbackStart}";
            _prefixedAtrFieldTransactionId = $"{_atrRoot}.{TransactionFields.AtrFieldTransactionId}";
            _prefixedAtrFieldDurability = $"{_atrRoot}.{TransactionFields.AtrFieldDurability}";
            _logger = loggerFactory.CreateLogger<AtrRepository>();
            _logger.LogDebug("Requested Durability = {durability}", atrDurability);
            _atrDurability = atrDurability ?? DurabilityLevel.Majority;
        }

        public override Task<AtrEntry?> FindEntryForTransaction(ICouchbaseCollection atrCollection, string atrId, string? attemptId = null)
            => FindEntryForTransaction(atrCollection, atrId, attemptId ?? _attemptId, _overallContext?.Config?.KeyValueTimeout);

        public static async Task<AtrEntry?> FindEntryForTransaction(
            ICouchbaseCollection atrCollection,
            string atrId,
            string attemptId,
            TimeSpan? keyValueTimeout = null
            )
        {
            _ = atrCollection ?? throw new ArgumentNullException(nameof(atrCollection));
            _ = atrId ?? throw new ArgumentNullException(nameof(atrId));

            var lookupInResult = await atrCollection.LookupInAsync(atrId,
                specs => specs.Get(TransactionFields.AtrFieldAttempts, isXattr: true),
                opts => opts.Defaults(keyValueTimeout).AccessDeleted(true)).CAF();

            if (!lookupInResult.Exists(0))
            {
                return null;
            }

            var asJson = lookupInResult.ContentAs<JObject>(0);
            if (asJson?.TryGetValue(attemptId, out var entry) == true)
            {
                var atrEntry = AtrEntry.CreateFrom(entry);
                if (atrEntry?.Cas == null && atrEntry?.State == default)
                {
                    throw new InvalidOperationException("ATR could not be parsed.");
                }

                return atrEntry;
            }
            else
            {
                return null;
            }
        }

        public static async Task<ICouchbaseCollection?> GetAtrCollection(AtrRef atrRef, ICouchbaseCollection anyCollection)
        {
            if (atrRef.BucketName == null || atrRef.CollectionName == null)
            {
                return null;
            }

            _ = anyCollection?.Scope?.Bucket?.Name ??
                throw new ArgumentOutOfRangeException(nameof(anyCollection), "Collection was not populated.");

            if (anyCollection.Scope.Name == atrRef.ScopeName
                && anyCollection.Scope.Bucket.Name == atrRef.BucketName
                && anyCollection.Name == atrRef.CollectionName)
            {
                return anyCollection;
            }

            var bkt = await anyCollection.Scope.Bucket.Cluster.BucketAsync(atrRef.BucketName).CAF();
            var scp = atrRef.ScopeName != null ? bkt.Scope(atrRef.ScopeName) : bkt.DefaultScope();

            return scp.Collection(atrRef.CollectionName);
        }

        public override Task<ICouchbaseCollection?> GetAtrCollection(AtrRef atrRef) => GetAtrCollection(atrRef, Collection);

        public override async Task MutateAtrComplete()
        {
            using var logScope = _logger.BeginMethodScope();
            var specs = new[]
            {
                MutateInSpec.Remove(_atrRoot, isXattr: true)
            };

            _ = await Collection.MutateInAsync(AtrId, specs, GetMutateOpts(StoreSemantics.Replace)).CAF();
            _logger.LogInformation("Removed ATR {atr}/{atrRoot} on {atrCollection} ", AtrId, _atrRoot, Collection.MakeKeyspace());
        }

        public override async Task MutateAtrPending(ulong exp, DurabilityLevel documentDurability)
        {
            using var logScope = _logger.BeginMethodScope();
            var shortDurability = new ShortStringDurabilityLevel(documentDurability).ToString();
            var content = new byte?[] { 0 };

            var specs = new[]
            {
                MutateInSpec.Insert(_prefixedAtrFieldTransactionId,
                    _overallContext.TransactionId, createPath: true, isXattr: true),
                MutateInSpec.Insert(_prefixedAtrFieldStatus,
                            nameof(AttemptStates.PENDING), isXattr: true),
                MutateInSpec.Insert(_prefixedAtrFieldStartTimestamp, MutationMacro.Cas),
                MutateInSpec.Insert(_prefixedAtrFieldExpiresAfterMsecs, exp,
                            createPath: false, isXattr: true),
                MutateInSpec.Insert(_prefixedAtrFieldDurability, shortDurability, isXattr: true),
                MutateInSpec.SetDoc(content), // ExtBinaryMetadata
            };
            var userFlags = new Flags { Compression = Core.IO.Operations.Compression.None, DataFormat = DataFormat.Binary, TypeCode = TypeCode.Object};

            var mutateResult = await Collection.MutateInAsync(AtrId, specs, GetMutateOpts(StoreSemantics.Upsert).Flags(userFlags)).CAF();
            _logger.LogInformation("Upserted ATR to PENDING {atr}/{atrRoot} (cas = {cas})", AtrId, _atrRoot, mutateResult.Cas);
        }

        public override async Task MutateAtrCommit(IEnumerable<StagedMutation> stagedMutations)
        {
            using var logScope = _logger.BeginMethodScope();
            (var inserts, var replaces, var removes) = SplitMutationsForStaging(stagedMutations);

            var specs = new []
            {
                MutateInSpec.Upsert(_prefixedAtrFieldStatus,
                    AttemptStates.COMMITTED.ToString(), isXattr: true),
                MutateInSpec.Upsert(_prefixedAtrFieldStartCommit, MutationMacro.Cas, isXattr: true),
                MutateInSpec.Upsert(_prefixedAtrFieldDocsInserted, inserts,
                    isXattr: true),
                MutateInSpec.Upsert(_prefixedAtrFieldDocsReplaced, replaces,
                    isXattr: true),
                MutateInSpec.Upsert(_prefixedAtrFieldDocsRemoved, removes,
                    isXattr: true),
                MutateInSpec.Insert(_prefixedAtrFieldsPendingSentinel, 0,
                    isXattr: true)
            };

            var mutateResult = await Collection.MutateInAsync(AtrId, specs, GetMutateOpts(StoreSemantics.Replace)).CAF();
            _logger.LogDebug("Updated to COMMITTED ATR {atr}/{atrRoot} (cas = {cas})", AtrId, _atrRoot, mutateResult.Cas);
        }

        public override async Task MutateAtrAborted(IEnumerable<StagedMutation> stagedMutations)
        {
            using var logScope = _logger.BeginMethodScope();
            (var inserts, var replaces, var removes) = SplitMutationsForStaging(stagedMutations);

            var specs = new MutateInSpec[]
            {
                MutateInSpec.Upsert(_prefixedAtrFieldStatus, AttemptStates.ABORTED.ToString(), isXattr: true),
                MutateInSpec.Upsert(_prefixedAtrFieldTimestampRollbackStart, MutationMacro.Cas, isXattr: true),
                MutateInSpec.Upsert(_prefixedAtrFieldDocsInserted, inserts, isXattr: true),
                MutateInSpec.Upsert(_prefixedAtrFieldDocsReplaced, replaces, isXattr: true),
                MutateInSpec.Upsert(_prefixedAtrFieldDocsRemoved, removes, isXattr: true),
            };

            var mutateResult = await Collection.MutateInAsync(AtrId, specs, GetMutateOpts(StoreSemantics.Replace)).CAF();
            _logger.LogDebug("Updated to ABORTED ATR {atr}/{atrRoot} (cas = {cas})", AtrId, _atrRoot, mutateResult.Cas);
        }

        public override async Task MutateAtrRolledBack()
        {
            using var logScope = _logger.BeginMethodScope();
            var specs = new MutateInSpec[]
            {
                MutateInSpec.Remove(_atrRoot, isXattr: true),
            };

            var mutateResult = await Collection.MutateInAsync(AtrId, specs, GetMutateOpts(StoreSemantics.Replace)).CAF();
            _logger.LogDebug("Removed ATR {atr}/{atrRoot} (cas = {cas})", AtrId, _atrRoot, mutateResult.Cas);
        }

        public override async Task<string?> LookupAtrState()
        {
            var lookupInResult = await Collection!.LookupInAsync(AtrId,
                    specs => specs.Get(_prefixedAtrFieldStatus, isXattr: true),
                    opts => GetLookupOpts().AccessDeleted(true))
                .CAF();
            var refreshedStatus = lookupInResult.ContentAs<string>(0);
            return refreshedStatus;
        }

        private (JArray inserts, JArray replaces, JArray removes) SplitMutationsForStaging(IEnumerable<StagedMutation> stagedMutations)
        {
            var mutations = stagedMutations.ToList();
            var stagedInserts = mutations.Where(sm => sm.Type == StagedMutationType.Insert);
            var stagedReplaces = mutations.Where(sm => sm.Type == StagedMutationType.Replace);
            var stagedRemoves = mutations.Where(sm => sm.Type == StagedMutationType.Remove);
            var inserts = new JArray(stagedInserts.Select(sm => sm.ForAtr()));
            var replaces = new JArray(stagedReplaces.Select(sm => sm.ForAtr()));
            var removes = new JArray(stagedRemoves.Select(sm => sm.ForAtr()));
            return (inserts, replaces, removes);
        }

        private LookupInOptions GetLookupOpts() => new LookupInOptions()
            .Defaults(_overallContext.Config.KeyValueTimeout).Serializer(Transactions.MetadataSerializer);

        private MutateInOptions GetMutateOpts(StoreSemantics storeSemantics) => new MutateInOptions()
            .Defaults(_atrDurability, _overallContext.Config.KeyValueTimeout)
            .Transcoder(Transactions.MetadataTranscoder)
            .StoreSemantics(storeSemantics);
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
