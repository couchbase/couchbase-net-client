#nullable enable
using System;
using System.Threading.Tasks;
using Couchbase.Core.Compatibility;

#pragma warning disable CS1591

namespace Couchbase.Integrated.Transactions.Internal.Test
{
    /// <summary>
    /// Protected hooks purely for testing purposes.  If you're an end-user looking at these for any reason, then
    /// please contact us first about your use-case: we are always open to adding good ideas into the transactions
    /// library.
    /// </summary>
    /// <remarks>All methods have default no-op implementations.</remarks>
    [InterfaceStability(Level.Volatile)]
    internal interface ITestHooks
    {


        Task<int?> BeforeAtrCommit(AttemptContext self);

        Task<int?> AfterAtrCommit(AttemptContext self);

        Task<int?> BeforeDocCommitted(AttemptContext self, string id);

        Task<int?> BeforeDocRolledBack(AttemptContext self, string id);

        Task<int?> AfterDocCommittedBeforeSavingCas(AttemptContext self, string id);

        Task<int?> AfterDocCommitted(AttemptContext self, string id);

        Task<int?> AfterDocsCommitted(AttemptContext self);

        Task<int?> BeforeDocRemoved(AttemptContext self, string id);

        Task<int?> AfterDocRemovedPreRetry(AttemptContext self, string id);

        Task<int?> AfterDocRemovedPostRetry(AttemptContext self, string id);

        Task<int?> AfterDocsRemoved(AttemptContext self);

        Task<int?> BeforeAtrPending(AttemptContext self);

        Task<int?> AfterAtrPending(AttemptContext self);

        Task<int?> AfterAtrComplete(AttemptContext self);

        Task<int?> BeforeAtrComplete(AttemptContext self);

        Task<int?> BeforeAtrRolledBack(AttemptContext self);

        Task<int?> AfterGetComplete(AttemptContext self, string id);

        Task<int?> BeforeRollbackDeleteInserted(AttemptContext self, string id);

        Task<int?> AfterStagedReplaceComplete(AttemptContext self, string id);

        Task<int?> AfterStagedRemoveComplete(AttemptContext self, string id);

        Task<int?> BeforeStagedInsert(AttemptContext self, string id);

        Task<int?> BeforeStagedRemove(AttemptContext self, string id);

        Task<int?> BeforeStagedReplace(AttemptContext self, string id);

        Task<int?> AfterStagedInsertComplete(AttemptContext self, string id);

        Task<int?> BeforeGetAtrForAbort(AttemptContext self);

        Task<int?> BeforeAtrAborted(AttemptContext self);

        Task<int?> AfterAtrAborted(AttemptContext self);

        Task<int?> AfterAtrRolledBack(AttemptContext self);

        Task<int?> AfterRollbackReplaceOrRemove(AttemptContext self, string id);

        Task<int?> AfterRollbackDeleteInserted(AttemptContext self, string id);

        Task<int?> BeforeRemovingDocDuringStagedInsert(AttemptContext self, string id);

        Task<int?> BeforeCheckAtrEntryForBlockingDoc(AttemptContext self, string id);

        Task<int?> BeforeDocGet(AttemptContext self, string id);

        Task<int?> BeforeGetDocInExistsDuringStagedInsert(AttemptContext self, string id);

        bool HasExpiredClientSideHook(AttemptContext self, string place, string? docId);
        Task<int?> BeforeAtrCommitAmbiguityResolution(AttemptContext attemptContext);

        Task<string?> AtrIdForVBucket(AttemptContext self, int vbucketId);

        Task<int?> BeforeQuery(AttemptContext self, string statement);
        Task<int?> AfterQuery(AttemptContext self, string statement);
        Task<int?> BeforeOverwritingStagedInsertRemoval(AttemptContext self, string id);

        Task<int?> BeforeRemoveStagedInsert(AttemptContext self, string id);
        Task<int?> AfterRemoveStagedInsert(AttemptContext self, string id);
    }

    /// <summary>
    /// Implementation of ITestHooks that relies on default interface implementation.
    /// </summary>
    internal class DefaultTestHooks : ITestHooks
    {
        public static readonly ITestHooks Instance = new DefaultTestHooks();
        public const string HOOK_ROLLBACK = "rollback";
        public const string HOOK_GET = "get";
        public const string HOOK_INSERT = "insert";
        public const string HOOK_REPLACE = "replace";
        public const string HOOK_REMOVE = "remove";
        public const string HOOK_BEFORE_COMMIT = "commit";
        public const string HOOK_ABORT_GET_ATR = "abortGetAtr"; // No references in Java code.
        public const string HOOK_ROLLBACK_DOC = "rollbackDoc";
        public const string HOOK_DELETE_INSERTED = "deleteInserted";
        public const string HOOK_CREATE_STAGED_INSERT = "createdStagedInsert";
        public const string HOOK_INSERT_QUERY = "insertQuery";
        public const string HOOK_REMOVE_DOC = "removeDoc";
        public const string HOOK_COMMIT_DOC = "commitDoc";
        public const string HOOK_QUERY = "query";
        public const string HOOK_ATR_COMMIT = "atrCommit";
        public const string HOOK_ATR_COMMIT_AMBIGUITY_RESOLUTION = "atrCommitAmbiguityResolution";
        public const string HOOK_ATR_ABORT = "atrAbort";
        public const string HOOK_ATR_ROLLBACK_COMPLETE = "atrRollbackComplete";
        public const string HOOK_ATR_PENDING = "atrPending";
        public const string HOOK_ATR_COMPLETE = "atrComplete";
        public const string HOOK_CHECK_WRITE_WRITE_CONFLICT = "checkATREntryForBlockingDoc";
        public const string HOOK_BEFORE_QUERY = "beforeQuery";
        public const string HOOK_AFTER_QUERY = "afterQuery";
        public const string HOOK_QUERY_BEGIN_WORK = "queryBeginWork";
        public const string HOOK_QUERY_COMMIT = "queryCommit";
        public const string HOOK_QUERY_KV_GET = "queryKvGet";
        public const string HOOK_QUERY_KV_REPLACE = "queryKvReplace";
        public const string HOOK_QUERY_KV_REMOVE = "queryKvRemove";
        public const string HOOK_QUERY_KV_INSERT = "queryKvInsert";
        public const string HOOK_QUERY_ROLLBACK = "queryRollback";
        public Task<int?> BeforeAtrCommit(AttemptContext self) => Task.FromResult<int?>(0);

        public Task<int?> AfterAtrCommit(AttemptContext self) => Task.FromResult<int?>(0);

        public Task<int?> BeforeDocCommitted(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public Task<int?> BeforeDocRolledBack(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public Task<int?> AfterDocCommittedBeforeSavingCas(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public Task<int?> AfterDocCommitted(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public Task<int?> AfterDocsCommitted(AttemptContext self) => Task.FromResult<int?>(0);

        public Task<int?> BeforeDocRemoved(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public Task<int?> AfterDocRemovedPreRetry(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public Task<int?> AfterDocRemovedPostRetry(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public Task<int?> AfterDocsRemoved(AttemptContext self) => Task.FromResult<int?>(0);

        public Task<int?> BeforeAtrPending(AttemptContext self) => Task.FromResult<int?>(0);

        public Task<int?> AfterAtrPending(AttemptContext self) => Task.FromResult<int?>(0);

        public Task<int?> AfterAtrComplete(AttemptContext self) => Task.FromResult<int?>(0);

        public Task<int?> BeforeAtrComplete(AttemptContext self) => Task.FromResult<int?>(0);

        public Task<int?> BeforeAtrRolledBack(AttemptContext self) => Task.FromResult<int?>(0);

        public Task<int?> AfterGetComplete(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public Task<int?> BeforeRollbackDeleteInserted(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public Task<int?> AfterStagedReplaceComplete(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public Task<int?> AfterStagedRemoveComplete(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public Task<int?> BeforeStagedInsert(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public Task<int?> BeforeStagedRemove(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public Task<int?> BeforeStagedReplace(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public Task<int?> AfterStagedInsertComplete(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public Task<int?> BeforeGetAtrForAbort(AttemptContext self) => Task.FromResult<int?>(0);

        public Task<int?> BeforeAtrAborted(AttemptContext self) => Task.FromResult<int?>(0);

        public Task<int?> AfterAtrAborted(AttemptContext self) => Task.FromResult<int?>(0);

        public Task<int?> AfterAtrRolledBack(AttemptContext self) => Task.FromResult<int?>(0);

        public Task<int?> AfterRollbackReplaceOrRemove(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public Task<int?> AfterRollbackDeleteInserted(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public Task<int?> BeforeRemovingDocDuringStagedInsert(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public Task<int?> BeforeCheckAtrEntryForBlockingDoc(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public Task<int?> BeforeDocGet(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public Task<int?> BeforeGetDocInExistsDuringStagedInsert(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public bool HasExpiredClientSideHook(AttemptContext self, string place, string? docId) => false;
        public Task<int?> BeforeAtrCommitAmbiguityResolution(AttemptContext attemptContext) => Task.FromResult<int?>(0);

        public Task<string?> AtrIdForVBucket(AttemptContext self, int vbucketId) => Task.FromResult<string?>(null);

        public Task<int?> BeforeQuery(AttemptContext self, string statement) => Task.FromResult<int?>(0);
        public Task<int?> AfterQuery(AttemptContext self, string statement) => Task.FromResult<int?>(0);
        public Task<int?> BeforeOverwritingStagedInsertRemoval(AttemptContext self, string id) => Task.FromResult<int?>(0);

        public Task<int?> BeforeRemoveStagedInsert(AttemptContext self, string id) => Task.FromResult<int?>(0);
        public Task<int?> AfterRemoveStagedInsert(AttemptContext self, string id) => Task.FromResult<int?>(0);
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





