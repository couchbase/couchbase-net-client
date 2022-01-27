using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Transactions.old.Config;
using Couchbase.Utils;

namespace Couchbase.Transactions.old
{
    internal class AttemptContext : IAttemptContext
    {
        private const string TransactionInterfacePrefix = "txn";
        private const string AtrStatusFieldName = "txn.st";
        private const string AtrStartTimestampFieldName = "txn.tst";
        private const string AtrExpirySecondsFieldName = "txn.exp";
        private const string AtrIdFieldName = "txn.atr_id";
        private const string AtrBucketNameFieldName = "txn.atr_bkt";
        private const string StagedVersionFieldName = "txn.ver";
        private const string StagedDataFieldName = "txn.staged";
        private const string RemovedStagedData = "<<REMOVED>>";
        private const string MutationCasMacro = "${Mutation.CAS}";

        public string TransactionId { get; }
        public string AttemptId { get; } = Guid.NewGuid().ToString();
        public string AtrId = string.Empty;
        public IBucket AtrBucket { get; private set; }
        public AttemptState State { get; private set; }

        private readonly TransactionConfig _config;
        private readonly IDictionary<string, ITransactionDocument> _stagedInserts = new Dictionary<string, ITransactionDocument>();
        private readonly IDictionary<string, ITransactionDocument> _stagedReplaces = new Dictionary<string, ITransactionDocument>();
        private readonly IDictionary<string, ITransactionDocument> _stagedRemoves = new Dictionary<string, ITransactionDocument>();

        public AttemptContext(string transactionId, TransactionConfig config)
        {
            TransactionId = transactionId;
            _config = config;
        }

        public async Task<ITransactionDocument<T>> Get<T>(IBucket bucket, string key)
        {
            if (TryGetStagedWrite(key, out var doc))
            {
                // document has been mutated within transaction, return this version
                return await Task.FromResult((ITransactionDocument<T>)doc).ConfigureAwait(false);
            }

            if (TryGetStagedRemove(key))
            {
                // document has been removed within transaction, return nothing
                return null;
            }

            var result = await bucket.LookupIn<T>(key)
                .Get(AtrIdFieldName, SubdocPathFlags.Xattr)
                .Get(AtrBucketNameFieldName, SubdocPathFlags.Xattr)
                .Get(StagedVersionFieldName, SubdocPathFlags.Xattr)
                .Get(StagedDataFieldName, SubdocPathFlags.Xattr)
                .Get(string.Empty) // get document body
                .WithTimeout(_config.KeyValueTimeout)
                .ExecuteAsync()
                .ConfigureAwait(false);

            if (!result.Success)
            {
                //TODO: key not found
                return null;
            }

            // create TransactionDocument
            return new TransactionDocument<T>(
                bucket,
                result.Id,
                result.Cas,
                result.Content<T>(4), // document body
                TransactionDocumentStatus.Normal,
                new TransactionLinks<T>(
                    result.Content<string>(0), // atrId
                    result.Content<string>(1), // atrBucketName
                    result.Content<string>(2), // stagedVersion
                    result.Content<T>(3)  // stagedContent
                )
            );
        }

        public async Task<ITransactionDocument<T>> Insert<T>(IBucket bucket, string key, T content)
        {
            TryGetStagedWrite(key, out var document);

            await UpdateAtr(bucket, key).ConfigureAwait(false);

            var result = await bucket.MutateIn<T>(key)
                .Upsert(AtrIdFieldName, AtrId, SubdocPathFlags.CreatePath | SubdocPathFlags.Xattr, SubdocDocFlags.InsertDocument)
                .Upsert(AtrBucketNameFieldName, AtrBucket.Name, SubdocPathFlags.Xattr)
                .Upsert(StagedVersionFieldName, AttemptId, SubdocPathFlags.Xattr)
                .Upsert(StagedDataFieldName, content, SubdocPathFlags.Xattr)
                .WithDurability(_config.PersistTo, _config.ReplicateTo)
                .WithTimeout(_config.KeyValueTimeout)
                .ExecuteAsync()
                .ConfigureAwait(false);

            if (!result.Success)
            {
                //TODO: failed to insert
                return null;
            }

            // create transaction document
            document = new TransactionDocument<T>(
                bucket,
                key, 
                result.Cas, 
                content,
                TransactionDocumentStatus.OwnWrite,
                new TransactionLinks<T>(
                    AtrId, 
                    AtrBucket.Name, 
                    AttemptId, 
                    content
                )
            );

            // add to staged inserts
            _stagedInserts[document.Key] = document;
            return (ITransactionDocument<T>)document;
        }

        public async Task<ITransactionDocument<T>> Replace<T>(IBucket bucket, ITransactionDocument<T> document)
        {
            await UpdateAtr(bucket, document.Key).ConfigureAwait(false);

            var result = await bucket.MutateIn<T>(document.Key)
                .Upsert(AtrIdFieldName, AtrId, SubdocPathFlags.Xattr)
                .Upsert(AtrBucketNameFieldName, AtrBucket.Name, SubdocPathFlags.Xattr)
                .Upsert(StagedVersionFieldName, AttemptId, SubdocPathFlags.Xattr)
                .Upsert(StagedDataFieldName, document.Content, SubdocPathFlags.Xattr)
                .WithCas(document.Cas)
                .WithDurability(_config.PersistTo, _config.ReplicateTo)
                .WithTimeout(_config.KeyValueTimeout)
                .ExecuteAsync()
                .ConfigureAwait(false);

            if (!result.Success)
            {
                //TODO: failed to replace
                return null;
            }

            // update transaction document CAS and add to update staged replaces
            document.Cas = result.Cas;
            _stagedReplaces[document.Key] = document;
            return document;
        }

        public async Task Remove(IBucket bucket, ITransactionDocument document)
        {
            await UpdateAtr(bucket, document.Key).ConfigureAwait(false);

            var result = await bucket.MutateIn<dynamic>(document.Key)
                .Upsert(AtrIdFieldName, AtrId, SubdocPathFlags.Xattr)
                .Upsert(AtrBucketNameFieldName, AtrBucket.Name, SubdocPathFlags.Xattr)
                .Upsert(StagedVersionFieldName, AttemptId, SubdocPathFlags.Xattr)
                .Upsert(StagedDataFieldName, RemovedStagedData, SubdocPathFlags.Xattr)
                .WithCas(document.Cas)
                .WithDurability(_config.PersistTo, _config.ReplicateTo)
                .WithTimeout(_config.KeyValueTimeout)
                .ExecuteAsync()
                .ConfigureAwait(false);

            if (!result.Success)
            {
                //TODO: failed to remove
                return;
            }

            // update transaction document CAS and add to staged removes
            document.Cas = result.Cas;
            _stagedRemoves[document.Key] = document;
        }

        public async Task Commit()
        {
            if (string.IsNullOrWhiteSpace(AtrId) || AtrBucket == null)
            {
                // no mutations
                State = AttemptState.Completed;
                return;
            }

            var result = await AtrBucket.MutateIn<dynamic>(AtrId)
                .Upsert($"attempts.{AttemptId}.st", AttemptState.Committed.GetDescription(), SubdocPathFlags.Xattr)
                .Upsert($"attempts.{AttemptId}.tsc", MutationCasMacro, SubdocPathFlags.Xattr | SubdocPathFlags.ExpandMacroValues)
                //.Upsert($"attempts.{AttemptId}.ins", CreateCommitData(_stagedInserts), SubdocPathFlags.Xattr)
                //.Upsert($"attempts.{AttemptId}.rep", CreateCommitData(_stagedReplaces), SubdocPathFlags.Xattr)
                //.Upsert($"attempts.{AttemptId}.rem", CreateCommitData(_stagedRemoves), SubdocPathFlags.Xattr)
                .ExecuteAsync()
                .ConfigureAwait(false);

            if (!result.Success)
            {
                //TODO: Failed to update ATR record
            }

            State = AttemptState.Committed;

            var tasks = new List<Task>();
            //tasks.AddRange(
            //    _stagedInserts.Concat(_stagedReplaces)
            //        .Select(x => new KeyValuePair<string, TransactionDocument<dynamic>>(x.Key, x.Value as TransactionDocument<dynamic>))
            //    .Select<KeyValuePair<string, TransactionDocument<dynamic>>, Task>(entry =>
            //    {
            //        return entry.Value.Bucket.MutateIn<dynamic>(entry.Key)
            //            .Remove(TransactionInterfacePrefix, SubdocPathFlags.Xattr)
            //            .Upsert(string.Empty, entry.Value.Content)
            //            .
            //            .ExecuteAsync();
            //    })
            //);
            //tasks.AddRange(
            //    _stagedRemoves
            //    .Select(x => new KeyValuePair<string, TransactionDocument<dynamic>>(x.Key, x.Value as TransactionDocument<dynamic>))
            //    .Select<KeyValuePair<string, TransactionDocument<dynamic>>, Task>(entry =>
            //    {
            //        return entry.Value.Bucket.RemoveAsync(entry.Key);
            //    })
            //);

            foreach (var entry in _stagedInserts)
            {
                var document = (entry.Value as TransactionDocument<dynamic>);
                var task = document.Bucket.MutateIn<dynamic>(entry.Key)
                    .Remove(TransactionInterfacePrefix, SubdocPathFlags.Xattr)
                    .Upsert(document.Content)
                    .ExecuteAsync();
                tasks.Add(task);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        public Task Rollback()
        {
            throw new NotImplementedException();
        }

        private static dynamic CreateCommitData(IDictionary<string, ITransactionDocument> entries)
        {
            return entries
                .Cast<KeyValuePair<string, ITransactionDocumentWithBucket>>()
                .Select(x => new {key = x.Key, value = x.Value.BucketName})
                .ToDictionary(kvp => kvp.key, kvp => kvp.value);
        }

        private async Task UpdateAtr(IBucket bucket, string key)
        {
            if (string.IsNullOrWhiteSpace(AtrId))
            {
                AtrId = AtrIdsHelper.GetAtrId(key);
                AtrBucket = bucket;
                State = AttemptState.Pending;

                var result = await AtrBucket.MutateIn<dynamic>(AtrId)
                    .Upsert($"attempts.{AttemptId}.st", AttemptState.Pending.GetDescription(),
                        SubdocPathFlags.CreatePath | SubdocPathFlags.Xattr, SubdocDocFlags.UpsertDocument)
                    .Upsert($"attempts.{AttemptId}.tst", MutationCasMacro,
                        SubdocPathFlags.Xattr | SubdocPathFlags.ExpandMacroValues)
                    .Upsert($"attempts.{AttemptId}.exp", _config.Expiration.TotalMilliseconds, 
                        SubdocPathFlags.Xattr)
                    .WithDurability(_config.PersistTo, _config.ReplicateTo)
                    .WithTimeout(_config.KeyValueTimeout)
                    .ExecuteAsync()
                    .ConfigureAwait(false);

                if (!result.Success)
                {
                    //TODO: Failed to create ATR
                }
            }
        }

        private bool TryGetStagedWrite(string key, out ITransactionDocument document)
        {
            if (_stagedInserts.TryGetValue(key, out var item))
            {
                document = item;
                return true;
            }

            if (_stagedReplaces.TryGetValue(key, out item))
            {
                document = item;
                return true;
            }

            document = default(TransactionDocument<object>);
            return false;
        }

        private bool TryGetStagedRemove(string key)
        {
            return _stagedRemoves.ContainsKey(key);
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
