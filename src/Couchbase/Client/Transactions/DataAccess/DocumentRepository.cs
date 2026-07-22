#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;
using Couchbase.Client.Transactions.Components;
using Couchbase.Client.Transactions.DataModel;
using Couchbase.Client.Transactions.Forwards;
using Couchbase.Client.Transactions.Internal;
using Couchbase.Client.Transactions.Support;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Operations;
using Couchbase.KeyValue.ZoneAware;
using Couchbase.Utils;
using System.Text.Json;
using StjSerializer = System.Text.Json.JsonSerializer;

namespace Couchbase.Client.Transactions.DataAccess
{
    internal class DocumentRepository : IDocumentRepository
    {
        private readonly TransactionContext _overallContext;
        private readonly TimeSpan? _keyValueTimeout;
        private readonly DurabilityLevel _durability;
        private readonly string _attemptId;

        /// <summary>
        /// Transcoder for user data during unstaging. Always wraps the user's serializer
        /// in a JsonTranscoder — binary content uses RawBinaryTranscoder instead (see UnstageInsertOrReplace).
        /// </summary>
        private readonly ITypeTranscoder _jsonUserDataTranscoder;

        public DocumentRepository(TransactionContext overallContext, TimeSpan? keyValueTimeout, DurabilityLevel durability, string attemptId, ITypeSerializer userDataSerializer)
        {
            _overallContext = overallContext;
            _keyValueTimeout = keyValueTimeout;
            _durability = durability;
            _attemptId = attemptId;

            _jsonUserDataTranscoder = new JsonTranscoder(userDataSerializer);
        }

        public async Task<(ulong updatedCas, MutationToken mutationToken)> MutateStagedInsert(ICouchbaseCollection collection, string docId, IContentAsWrapper content, string opId, IAtrRepository atr, ulong? cas = null, DateTimeOffset? expiry = null)
        {
            List<MutateInSpec> specs = CreateMutationSpecs(atr, "insert", content, opId, expiry: expiry);
            var opts = GetMutateInOptions(StoreSemantics.Insert, collection)
                .AccessDeleted(true)
                .CreateAsDeleted(true);

            // we always preserve TTLs - using SupportsCollections as a proxy for supporting TTLs
            opts.PreserveTtl(collection.Scope.Bucket.SupportsCollections);

            // we set the flags when staging the insert (though, we do it again when we unstage)
            if (SupportsReplaceBodyWithXattr(collection))
            {
                opts.Flags(content.Flags);
            }

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

        public async Task<(ulong updatedCas, MutationToken mutationToken)> MutateStagedReplace(TransactionGetResult doc, IContentAsWrapper content, string opId, IAtrRepository atr, bool accessDeleted, DateTimeOffset? expiry = null)
        {
            if (doc.Cas == 0)
            {
                throw new ArgumentOutOfRangeException("Document CAS should not be wildcard or default when replacing.");
            }
            var specs = CreateMutationSpecs(atr, "replace", content, opId, doc.DocumentMetadata, expiry);
            var opts = GetMutateInOptions(StoreSemantics.Replace, doc.Collection).Cas(doc.Cas);
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

            var txnAsJsonElement = StjSerializer.SerializeToElement(txn, Transactions.MetadataJsonOptions);

            var specs = new []
            {
                MutateInSpec.Upsert(TransactionFields.TransactionInterfacePrefixOnly, txnAsJsonElement, isXattr: true),
                MutateInSpec.Upsert(TransactionFields.Crc32, MutationMacro.ValueCRC32c, createPath: true, isXattr: true),
            };

            var opts = GetMutateInOptions(StoreSemantics.Replace, doc.Collection).Cas(doc.Cas).CreateAsDeleted(true);
            var updatedDoc = await doc.Collection.MutateInAsync(doc.Id, specs, opts).CAF();
            return (updatedDoc.Cas, updatedDoc.MutationToken);
        }

        public async Task<(ulong updatedCas, MutationToken mutationToken)> RemoveStagedInsert(TransactionGetResult doc)
        {
            var specs = new [] { MutateInSpec.Remove(TransactionFields.TransactionInterfacePrefixOnly, isXattr: true) };
            var opts = GetMutateInOptions(StoreSemantics.Replace, doc.Collection).Cas(doc.Cas).AccessDeleted(true);
            var updatedDoc = await doc.Collection.MutateInAsync(doc.Id, specs, opts).CAF();
            return (updatedDoc.Cas, updatedDoc.MutationToken);
        }

        public bool SupportsReplaceBodyWithXattr(ICouchbaseCollection collection)
        {
            var bucket = collection.Scope.Bucket;
            if (bucket is CouchbaseBucket couchBucket)
            {
                // NOTE: we look for SUBDOC_REVIVE_DOCUMENT as this came slightly later than
                // ReplaceBodyWithXattr, and both are needed.
                return couchBucket.CurrentConfig?.BucketCapabilities.Contains(BucketCapabilities
                    .SUBDOC_REVIVE_DOCUMENT) == true;
            }

            return false;
        }

        public async Task<(ulong updatedCas, MutationToken? mutationToken)> UnstageInsertOrReplace(ICouchbaseCollection collection, string docId, ulong cas, object finalDoc, bool insertMode, Flags flags, DateTimeOffset? expiry = null)
        {
            bool isBinary = flags.DataFormat == DataFormat.Binary;
            if (SupportsReplaceBodyWithXattr(collection))
            {
                MutateInOptions opts;
                if (insertMode)
                {
                    opts = GetMutateInOptions(StoreSemantics.AccessDeleted, collection)
                        .ReviveDocument(true).PreserveTtl(false);
                } else
                {
                    opts = GetMutateInOptions(StoreSemantics.Replace, collection)
                        .Cas(cas);
                }

                if (expiry.HasValue)
                {
                    opts.Expiry(expiry.Value.RemainingTtl());
                    opts.PreserveTtl(false);
                }
                opts.Transcoder(isBinary
                    ? new RawBinaryTranscoder()
                    : _jsonUserDataTranscoder);
                var stagedDataField = isBinary
                    ? TransactionFields.BinStagedData
                    : TransactionFields.StagedData;
                opts.Flags(flags);
                var mutateResult = await collection.MutateInAsync(docId, specs =>
                        specs.ReplaceBodyWithXattr(stagedDataField, isBinary)
                            .Upsert(TransactionFields.TransactionInterfacePrefixOnly, string.Empty,
                                isXattr: true, createPath: true)
                            .Remove(TransactionFields.TransactionInterfacePrefixOnly,
                                isXattr: true),
                    opts).CAF();
                return (mutateResult.Cas, mutateResult.MutationToken);
            }
            // if bucket doesn't support ReplaceBodyWithXattr (and ReviveDocument)
            if (insertMode)
            {
                // InsertAsync has no flags option; the persisted flags are whatever the transcoder's
                // GetFormat returns. Pin them to the staged flags so the committed doc keeps them.
                ITypeTranscoder insertTranscoder = isBinary
                    ? new RawBinaryTranscoder()
                    : _jsonUserDataTranscoder;
                var opts = new InsertOptions().Defaults(_durability, _keyValueTimeout)
                    .Transcoder(new FixedFlagsTranscoder(insertTranscoder, flags));
                if (expiry.HasValue)
                {
                    opts.Expiry(expiry.Value.RemainingTtl());
                }
                var mutateResult = await collection.InsertAsync(docId, finalDoc, opts).CAF();
                return (mutateResult.Cas, mutateResult.MutationToken);
            }
            else
            {
                var opts = GetMutateInOptions(StoreSemantics.Replace, collection)
                    .Cas(cas)
                    .Transcoder(isBinary
                        ? new RawBinaryTranscoder()
                        : _jsonUserDataTranscoder);
                if (expiry.HasValue)
                {
                    opts.Expiry(expiry.Value.RemainingTtl());
                    opts.PreserveTtl(false);
                }
                var mutateResult = await collection.MutateInAsync(docId, specs =>
                    specs.Upsert(TransactionFields.TransactionInterfacePrefixOnly, string.Empty,
                            isXattr: true, createPath: true)
                        .Remove(TransactionFields.TransactionInterfacePrefixOnly, isXattr: true)
                        .SetDoc(finalDoc), opts).CAF();
                return (mutateResult.Cas, mutateResult.MutationToken);
            }
        }

        public async Task UnstageRemove(ICouchbaseCollection collection, string docId, ulong cas = 0)
        {
            var opts = new RemoveOptions().Defaults(_durability, _keyValueTimeout).Cas(cas);
            await collection.RemoveAsync(docId, opts).CAF();
        }

        public async Task ClearTransactionMetadata(ICouchbaseCollection collection, string docId, ulong cas, bool isDeleted)
        {
            var opts = GetMutateInOptions(StoreSemantics.Replace, collection).Cas(cas);
            if (isDeleted)
            {
                opts.AccessDeleted(true);
            }

            var specs = new []
            {
                MutateInSpec.Upsert(TransactionFields.TransactionInterfacePrefixOnly, (string?)null, isXattr: true),
                MutateInSpec.Remove(TransactionFields.TransactionInterfacePrefixOnly, isXattr: true)
            };

            _ = await collection.MutateInAsync(docId, specs, opts).CAF();
        }

        public async Task<DocumentLookupResult> LookupDocumentAsync(ICouchbaseCollection collection,
            string docId, DateTimeOffset deadline, ITypeTranscoder? transcoder = null,
            bool allowReplica = false)
        {
            var timeout = deadline - DateTimeOffset.UtcNow;
            if (_keyValueTimeout != null && timeout > _keyValueTimeout)
                timeout = _keyValueTimeout.Value;
            return await LookupDocumentAsync(collection, docId, timeout, true, transcoder,
                allowReplica, _jsonUserDataTranscoder).CAF();
        }

        public async Task<DocumentLookupResult> LookupDocumentAsync(ICouchbaseCollection collection, string docId, bool fullDocument = true, ITypeTranscoder? transcoder = null, bool allowReplica = false) => await LookupDocumentAsync(collection, docId, _keyValueTimeout, fullDocument, transcoder, allowReplica, _jsonUserDataTranscoder).CAF();
        internal static async Task<DocumentLookupResult> LookupDocumentAsync(ICouchbaseCollection collection, string docId, TimeSpan? keyValueTimeout, bool fullDocument = true, ITypeTranscoder? userTranscoder = null, bool allowReplica = false, ITypeTranscoder? defaultJsonTranscoder = null)
        {
            var specs = new List<LookupInSpec>()
            {
                LookupInSpec.Get(TransactionFields.TransactionInterfacePrefixOnly, isXattr: true),
                LookupInSpec.Get("$document", isXattr: true),
                LookupInSpec.Get(TransactionFields.StagedData, isXattr: true),
                LookupInSpec.Get(TransactionFields.BinStagedData, isXattr: true, isBinary: true),
            };
            int? fullDocIndex = null;
            if (fullDocument)
            {
                specs.Add(LookupInSpec.GetFull());
                fullDocIndex = specs.Count - 1;
            }

            // The sub-doc lookup always uses the metadata transcoder (JSON with camelCase)
            // regardless of the user's content transcoder.
            ILookupInResult? lookupInResult;
            if (allowReplica)
            {
                var opts = new LookupInAnyReplicaOptions()
                    .Timeout(keyValueTimeout)
                    .Transcoder(Transactions.MetadataTranscoder)
                    .ReadPreference(InternalReadPreference.SelectedServerGroupWithFallback)
                    .AccessDeleted(true); // apply if supported
                lookupInResult =
                    await collection.LookupInAnyReplicaAsync(docId, specs, opts).CAF();
            }
            else
            {
                var opts = new LookupInOptions().Defaults(keyValueTimeout)
                    .AccessDeleted(true) // apply if supported
                    .Transcoder(Transactions.MetadataTranscoder);
                lookupInResult = await collection.LookupInAsync(docId, specs, opts).CAF();
            }

            int txnIndex = 0;
            int docMetaIndex = 1;
            int stagedDataIndex = 2;
            int stagedBinDataIndex = 3;

            var docMeta = lookupInResult.ContentAs<DocumentMetadata>(docMetaIndex);
            int dataIdx = stagedDataIndex; // just need a default

            // Deserialize the transaction xattr once; reused below for the staged user-flags
            // (txn.aux.uf) and stored on the result.
            var txnXattrs = lookupInResult.Exists(txnIndex)
                ? lookupInResult.ContentAs<TransactionXattrs>(txnIndex)
                : null;

            // Determine the appropriate transcoder for wrapping the document content.
            // This is separate from the lookup transcoder used above.
            // Note: We use separate transcoders for staged vs unstaged content because:
            // - Staged content format is determined by whether BinStagedData or StagedData exists
            // - Unstaged content format is unknown; we use JSON unless we know it's binary
            defaultJsonTranscoder ??= new JsonTranscoder(NonStreamingSerializerWrapper.FromCluster(collection.Scope.Bucket.Cluster));

            ITypeTranscoder stagedContentTranscoder;
            bool isBinaryStaged = false;
            if (lookupInResult.Exists(txnIndex))
            {
                // Determine which staged data field exists (if any).
                // For removes, neither StagedData nor BinStagedData exists.
                bool hasStagedData = lookupInResult.Exists(stagedDataIndex);
                bool hasBinStagedData = lookupInResult.Exists(stagedBinDataIndex);

                if (hasBinStagedData)
                {
                    dataIdx = stagedBinDataIndex;
                    isBinaryStaged = true;
                    stagedContentTranscoder = new RawBinaryTranscoder();
                }
                else if (hasStagedData)
                {
                    dataIdx = stagedDataIndex;
                    stagedContentTranscoder = userTranscoder ?? defaultJsonTranscoder;
                }
                else
                {
                    // Neither exists (e.g., remove operation) - use defaults
                    dataIdx = stagedDataIndex;
                    stagedContentTranscoder = userTranscoder ?? defaultJsonTranscoder;
                }
            }
            else
            {
                stagedContentTranscoder = userTranscoder ?? defaultJsonTranscoder;
            }

            // For unstaged (pre-transaction) content, we need to be careful:
            // - If the staged content is binary, the pre-transaction content was also binary
            // - Otherwise, use JSON transcoder as a safe default
            //   (the user's binary transcoder would fail on JSON pre-transaction content)
            ITypeTranscoder unstagedContentTranscoder = isBinaryStaged
                ? new RawBinaryTranscoder()
                : defaultJsonTranscoder;

            IContentAsWrapper? unstagedContent = fullDocIndex.HasValue
                ? new LookupInContentAsWrapper(lookupInResult, fullDocIndex.Value, unstagedContentTranscoder)
                : null;

            // Staged content carries the user flags recorded in txn.aux.uf when it was staged,
            // NOT the live document body's flags. This matters when committing content we didn't
            // stage (lost-transaction cleanup) or when resolving ambiguity from a re-read doc.
            var stagedContent = lookupInResult.Exists(dataIdx)
                ? new LookupInContentAsWrapper(lookupInResult, dataIdx, stagedContentTranscoder,
                    flagsOverride: ParseStagedUserFlags(txnXattrs))
                : null;

            var result = new DocumentLookupResult(docId,
                unstagedContent,
                stagedContent,
                lookupInResult,
                docMeta,
                collection);

            result.TransactionXattrs = txnXattrs;

            return result;
        }

        /// <summary>
        /// Reconstruct the user flags recorded in <c>txn.aux.uf</c> at staging time. Falls back to
        /// JSON common flags when the field is absent (e.g. staged by an older/other SDK that did
        /// not record it — such content was always JSON), mirroring Java's
        /// <c>stagedUserFlags().orElse(CodecFlags.JSON_COMMON_FLAGS)</c>.
        /// </summary>
        internal static Flags ParseStagedUserFlags(TransactionXattrs? txnXattrs)
        {
            if (txnXattrs?.AuxiliaryData is { ValueKind: JsonValueKind.Object } aux
                && aux.TryGetProperty("uf", out var ufElement)
                && ufElement.TryGetUInt32(out var uf))
            {
                return Flags.FromUInt32(uf);
            }

            return Flags.JsonCommonFlags;
        }

        private MutateInOptions GetMutateInOptions(StoreSemantics storeSemantics, ICouchbaseCollection collection) =>
            new MutateInOptions().Defaults(_durability, _keyValueTimeout)
                .Transcoder(Transactions.MetadataTranscoder)
                .StoreSemantics(storeSemantics)
                .PreserveTtl(collection.Scope.Bucket.SupportsCollections);

        private List<MutateInSpec> CreateMutationSpecs(IAtrRepository atr, string opType, IContentAsWrapper content, string opId, DocumentMetadata? dm = null, DateTimeOffset? expiry = null)
        {
            var specs = new List<MutateInSpec>
            {
                MutateInSpec.Upsert(TransactionFields.TransactionInterfacePrefixOnly, new { }, createPath: true, isXattr: true),
                MutateInSpec.Upsert(TransactionFields.TransactionId, _overallContext.TransactionId,
                    createPath: true, isXattr: true),
                MutateInSpec.Upsert(TransactionFields.AttemptId, _attemptId, createPath: true, isXattr: true),
                MutateInSpec.Upsert(TransactionFields.AtrId, atr.AtrId, createPath: true, isXattr: true),
                MutateInSpec.Upsert(TransactionFields.AtrScopeName, atr.ScopeName, createPath: true, isXattr: true),
                MutateInSpec.Upsert(TransactionFields.AtrBucketName, atr.BucketName, createPath: true, isXattr: true),
                MutateInSpec.Upsert(TransactionFields.AtrCollName, atr.CollectionName, createPath: true, isXattr: true),
                MutateInSpec.Upsert(TransactionFields.Type, opType, createPath: true, isXattr: true),
                MutateInSpec.Upsert(TransactionFields.Crc32, MutationMacro.ValueCRC32c, createPath: true, isXattr: true),
                MutateInSpec.Upsert(TransactionFields.OperationId, opId,true, true),
            };
            if (expiry.HasValue)
            {
                specs.Add(MutateInSpec.Upsert(TransactionFields.DocExpiry, expiry.Value.ToUnixTimeSeconds(),
                    true, true));
            }
            switch (opType)
            {
                case "remove":
                    specs.Add(MutateInSpec.Upsert(TransactionFields.StagedData, new { }, isXattr: true));
                    specs.Add(MutateInSpec.Remove(TransactionFields.StagedData, isXattr: true));
                    break;
                case "replace":
                case "insert":
                    // check flags, maybe we need to stage binary data, maybe not
                    if (content.Flags.DataFormat == DataFormat.Binary)
                    {
                        specs.Add(MutateInSpec.Upsert(TransactionFields.BinStagedData, content.ContentAs<byte[]>(),
                            createPath: true, isXattr: true, removeBrackets: false, isBinary: true));
                        specs.Add(MutateInSpec.Upsert(TransactionFields.ForwardCompatibility,
                            ForwardCompatibility.extBinSupport, createPath: true, isXattr: true));
                    }
                    else
                    {
                        // The content is already encoded by the user's serializer in TranscodedContentWrapper.
                        // Write it as raw JSON bytes — avoids decoding to object then re-encoding with the
                        // metadata serializer, which would break if the two serializers are different
                        // (e.g., System.Text.Json content decoded as JsonElement, then re-encoded by a different
                        // serializer would produce incorrect output instead of the actual JSON).
                        var stagedBytes = content.ContentAs<byte[]>()!;
                        var rawJsonElement = StjSerializer.Deserialize<JsonElement>(stagedBytes);
                        specs.Add(MutateInSpec.Upsert(TransactionFields.StagedData, rawJsonElement,
                            createPath: true, isXattr: true));
                    }
                    // record the user flags (reconstructed on read by Flags.FromUInt32)
                    var flagCompact = content.Flags.ToUInt32();
                    specs.Add(MutateInSpec.Upsert(TransactionFields.UserFlags, flagCompact,
                        createPath: true, isXattr: true));
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
