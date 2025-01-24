#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;
using Couchbase.Client.Transactions.Components;
using Couchbase.Client.Transactions.DataModel;
using Couchbase.Client.Transactions.Internal;
using Couchbase.Client.Transactions.Support;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace Couchbase.Client.Transactions.DataAccess
{
    internal class DocumentRepository : IDocumentRepository
    {
        private readonly TransactionContext _overallContext;
        private readonly TimeSpan? _keyValueTimeout;
        private readonly DurabilityLevel _durability;
        private readonly string _attemptId;
        private readonly JsonSerializer _metadataSerializer;
        private readonly ITypeTranscoder _userDataTranscoder;

        public DocumentRepository(TransactionContext overallContext, TimeSpan? keyValueTimeout, DurabilityLevel durability, string attemptId, Core.IO.Serializers.ITypeSerializer userDataSerializer)
        {
            _overallContext = overallContext;
            _keyValueTimeout = keyValueTimeout;
            _durability = durability;
            _attemptId = attemptId;


            var metadataSerializerSettings = new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore
            };

            _metadataSerializer = JsonSerializer.Create(metadataSerializerSettings);
            _userDataTranscoder = new JsonTranscoder(userDataSerializer);
        }

        public async Task<(ulong updatedCas, MutationToken mutationToken)> MutateStagedInsert(ICouchbaseCollection collection, string docId, object content, IAtrRepository atr, ulong? cas = null)
        {
            List<MutateInSpec> specs = CreateMutationSpecs(atr, "insert", content);
            var opts = GetMutateInOptions(StoreSemantics.Insert)
                .AccessDeleted(true)
                .CreateAsDeleted(true);

            if (cas.HasValue)
            {
                opts.Cas(cas.Value).StoreSemantics(StoreSemantics.Replace);
            }
            else
            {
                opts.Cas(0);
            }

            var mutateResult = await collection.MutateInAsync(docId, specs, opts).CAF();
            return (mutateResult.Cas, mutateResult.MutationToken);
        }

        public async Task<(ulong updatedCas, MutationToken mutationToken)> MutateStagedReplace(TransactionGetResult doc, object content, IAtrRepository atr, bool accessDeleted)
        {
            if (doc.Cas == 0)
            {
                throw new ArgumentOutOfRangeException("Document CAS should not be wildcard or default when replacing.");
            }
            var specs = CreateMutationSpecs(atr, "replace", content, doc.DocumentMetadata);
            var opts = GetMutateInOptions(StoreSemantics.Replace).Cas(doc.Cas);
            if (accessDeleted)
            {
                opts.AccessDeleted(true);
            }

            var updatedDoc = await doc.Collection.MutateInAsync(doc.Id, specs, opts).CAF();
            return (updatedDoc.Cas, updatedDoc.MutationToken);
        }

        public async Task<(ulong updatedCas, MutationToken mutationToken)> MutateStagedRemove(TransactionGetResult doc, IAtrRepository atr)
        {
            // For ExtAllKvCombinations, the Java implementation was updated to write "txn" as one JSON blob instead of multiple MutateInSpecs.
            // Remove is the one where it had to be updated, given that we need to remove the staged data only if it exists.
            var txn = new TransactionXattrs()
            {
                Operation = new StagedOperation() { Type = "remove" },
                Id = new CompositeId() { Transactionid = _overallContext.TransactionId, AttemptId = _attemptId },
                AtrRef = new AtrRef() { Id = atr.AtrId, ScopeName = atr.ScopeName, BucketName = atr.BucketName, CollectionName = atr.CollectionName },
                RestoreMetadata = doc.DocumentMetadata
            };

            var txnAsJObject = Newtonsoft.Json.Linq.JObject.FromObject(txn, _metadataSerializer);

            var specs = new MutateInSpec[]
            {
                MutateInSpec.Upsert(TransactionFields.TransactionInterfacePrefixOnly, txnAsJObject, isXattr: true),
                MutateInSpec.Upsert(TransactionFields.Crc32, MutationMacro.ValueCRC32c, createPath: true, isXattr: true),
            };

            var opts = GetMutateInOptions(StoreSemantics.Replace).Cas(doc.Cas).CreateAsDeleted(true);
            var updatedDoc = await doc.Collection.MutateInAsync(doc.Id, specs, opts).CAF();
            return (updatedDoc.Cas, updatedDoc.MutationToken);
        }

        public async Task<(ulong updatedCas, MutationToken mutationToken)> RemoveStagedInsert(TransactionGetResult doc)
        {
            var specs = new MutateInSpec[] { MutateInSpec.Remove(TransactionFields.TransactionInterfacePrefixOnly, isXattr: true) };
            var opts = GetMutateInOptions(StoreSemantics.Replace).Cas(doc.Cas).AccessDeleted(true);
            var updatedDoc = await doc.Collection.MutateInAsync(doc.Id, specs, opts).CAF();
            return (updatedDoc.Cas, updatedDoc.MutationToken);
        }

        public async Task<(ulong updatedCas, MutationToken? mutationToken)> UnstageInsertOrReplace(ICouchbaseCollection collection, string docId, ulong cas, object finalDoc, bool insertMode)
        {
            if (insertMode)
            {
                var opts = new InsertOptions().Defaults(_durability, _keyValueTimeout);
                var mutateResult = await collection.InsertAsync(docId, finalDoc, opts).CAF();
                return (mutateResult.Cas, mutateResult?.MutationToken);
            }
            else
            {
                var opts = GetMutateInOptions(StoreSemantics.Replace)
                    .Cas(cas)
                    .Transcoder(_userDataTranscoder);
                var mutateResult = await collection.MutateInAsync(docId, specs =>
                            specs.Upsert(TransactionFields.TransactionInterfacePrefixOnly, string.Empty,
                                    isXattr: true, createPath: true)
                                .Remove(TransactionFields.TransactionInterfacePrefixOnly, isXattr: true)
                                .SetDoc(finalDoc), opts).CAF();
                return (mutateResult.Cas, mutateResult?.MutationToken);
            }
        }

        public async Task UnstageRemove(ICouchbaseCollection collection, string docId, ulong cas = 0)
        {
            var opts = new RemoveOptions().Defaults(_durability, _keyValueTimeout).Cas(cas);
            await collection.RemoveAsync(docId, opts).CAF();
        }

        public async Task ClearTransactionMetadata(ICouchbaseCollection collection, string docId, ulong cas, bool isDeleted)
        {
            var opts = GetMutateInOptions(StoreSemantics.Replace).Cas(cas);
            if (isDeleted)
            {
                opts.AccessDeleted(true);
            }

            var specs = new MutateInSpec[]
            {
                        MutateInSpec.Upsert(TransactionFields.TransactionInterfacePrefixOnly, (string?)null, isXattr: true),
                        MutateInSpec.Remove(TransactionFields.TransactionInterfacePrefixOnly, isXattr: true)
            };

            _ = await collection.MutateInAsync(docId, specs, opts).CAF();
        }

        public async Task<DocumentLookupResult> LookupDocumentAsync(ICouchbaseCollection collection, string docId, bool fullDocument = true) => await LookupDocumentAsync(collection, docId, _keyValueTimeout, fullDocument).CAF();
        internal static async Task<DocumentLookupResult> LookupDocumentAsync(ICouchbaseCollection collection, string docId, TimeSpan? keyValueTimeout, bool fullDocument = true)
        {
            var specs = new List<LookupInSpec>()
            {
                LookupInSpec.Get(TransactionFields.TransactionInterfacePrefixOnly, isXattr: true),
                LookupInSpec.Get("$document", isXattr: true),
                LookupInSpec.Get(TransactionFields.StagedData, isXattr: true)
            };

            //We use .Transcoder() instead of .Serializer() as LookupInResult uses the Transcoder's Serializer for ContentAs<T>
            var opts = new LookupInOptions().Defaults(keyValueTimeout).AccessDeleted(true).Transcoder(new JsonTranscoder(Transactions.MetadataSerializer));

            int? txnIndex = 0;
            int docMetaIndex = 1;
            int? stagedDataIndex = 2;

            int? fullDocIndex = null;
            if (fullDocument)
            {
                specs.Add(LookupInSpec.GetFull());
                fullDocIndex = specs.Count - 1;
            }

            ILookupInResult lookupInResult;
            try
            {
                lookupInResult = await collection.LookupInAsync(docId, specs, opts).CAF();
            }
            catch (PathInvalidException)
            {
                throw;
            }

            var docMeta = lookupInResult.ContentAs<DocumentMetadata>(docMetaIndex);

            IContentAsWrapper? unstagedContent = fullDocIndex.HasValue
                ? new LookupInContentAsWrapper(lookupInResult, fullDocIndex.Value)
                : null;

            var stagedContent = stagedDataIndex.HasValue && lookupInResult.Exists(stagedDataIndex.Value)
                ? new LookupInContentAsWrapper(lookupInResult, stagedDataIndex.Value)
                : null;

            var result = new DocumentLookupResult(docId,
                unstagedContent,
                stagedContent,
                lookupInResult,
                docMeta,
                collection);

            if (txnIndex.HasValue && lookupInResult.Exists(txnIndex.Value))
            {
                result.TransactionXattrs = lookupInResult.ContentAs<TransactionXattrs>(txnIndex.Value);
            }

            return result;
        }

        private MutateInOptions GetMutateInOptions(StoreSemantics storeSemantics) => new MutateInOptions().Defaults(_durability, _keyValueTimeout)
            .Transcoder(Transactions.MetadataTranscoder)
            .StoreSemantics(storeSemantics);

        private List<MutateInSpec> CreateMutationSpecs(IAtrRepository atr, string opType, object content, DocumentMetadata? dm = null)
        {
            // Round-trip the content through the user's specified serializer.
            object userSerializedContent = content;
            JRaw? userSerializedContentRaw = null;
            var userDataSerializer = _userDataTranscoder?.Serializer;
            if (userDataSerializer != null && userDataSerializer != Transactions.MetadataTranscoder.Serializer)
            {
                byte[] bytes = userDataSerializer.Serialize(content);
                userSerializedContent = userDataSerializer.Deserialize<object>(bytes)!;
                userSerializedContentRaw = new JRaw(userSerializedContent ?? content);
            }

            var specs = new List<MutateInSpec>
            {
                MutateInSpec.Upsert(TransactionFields.TransactionId, _overallContext.TransactionId,
                    createPath: true, isXattr: true),
                MutateInSpec.Upsert(TransactionFields.AttemptId, _attemptId, createPath: true, isXattr: true),
                MutateInSpec.Upsert(TransactionFields.AtrId, atr.AtrId, createPath: true, isXattr: true),
                MutateInSpec.Upsert(TransactionFields.AtrScopeName, atr.ScopeName, createPath: true, isXattr: true),
                MutateInSpec.Upsert(TransactionFields.AtrBucketName, atr.BucketName, createPath: true, isXattr: true),
                MutateInSpec.Upsert(TransactionFields.AtrCollName, atr.CollectionName, createPath: true, isXattr: true),
                MutateInSpec.Upsert(TransactionFields.Type, opType, createPath: true, isXattr: true),
                MutateInSpec.Upsert(TransactionFields.Crc32, MutationMacro.ValueCRC32c, createPath: true, isXattr: true),
            };

            switch (opType)
            {
                case "remove":
                    specs.Add(MutateInSpec.Upsert(TransactionFields.StagedData, new { }, isXattr: true));
                    specs.Add(MutateInSpec.Remove(TransactionFields.StagedData, isXattr: true));
                    break;
                case "replace":
                case "insert":
                    if (userSerializedContentRaw != null)
                    {
                        specs.Add(MutateInSpec.Upsert(TransactionFields.StagedData, userSerializedContentRaw, createPath: true, isXattr: true));
                    }
                    else
                    {
                        specs.Add(MutateInSpec.Upsert(TransactionFields.StagedData, userSerializedContent, createPath: true, isXattr: true));
                    }

                    break;
            }

            if (dm != null)
            {
                if (dm.Cas != null)
                {
                    specs.Add(MutateInSpec.Upsert(TransactionFields.PreTxnCas, dm.Cas, createPath: true, isXattr: true));
                }

                if (dm.RevId != null)
                {
                    specs.Add(MutateInSpec.Upsert(TransactionFields.PreTxnRevid, dm.RevId, createPath: true, isXattr: true));
                }

                if (dm.ExpTime != null)
                {
                    specs.Add(MutateInSpec.Upsert(TransactionFields.PreTxnExptime, dm.ExpTime, createPath: true, isXattr: true));
                }
            }

            return specs;
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
