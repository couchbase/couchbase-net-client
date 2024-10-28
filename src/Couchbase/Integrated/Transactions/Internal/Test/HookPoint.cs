using System.ComponentModel;

namespace Couchbase.Integrated.Transactions.Internal.Test;

// Generated from transactions-fit-performer/gRPC/hooks.transactions.proto
// since this doesn't change often and is only for test purposes, we keep it a manual process rather than integrating it
// into the build and pulling in another dependency on gRPC.
// * generate with the protoc command
// * replace [pbr::OriginalName(...)] with [Description(...)]
// * format document with IDE settings.

/// <summary>
/// Generally the BEFORE_ hook is used to inject an error, e.g. as if the server had returned a failure
/// The AFTER_ hook is used for ambiguity testing.  E.g. the op succeeded but FAIL_AMBIGUOUS was returned.
/// </summary>
public enum HookPoint
{
    [Description("BEFORE_ATR_COMMIT")] BeforeAtrCommit = 0,
    [Description("AFTER_ATR_COMMIT")] AfterAtrCommit = 1,
    [Description("BEFORE_DOC_COMMITTED")] BeforeDocCommitted = 2,

    [Description("BEFORE_DOC_ROLLED_BACK")]
    BeforeDocRolledBack = 3,

    [Description("AFTER_DOC_COMMITTED_BEFORE_SAVING_CAS")]
    AfterDocCommittedBeforeSavingCas = 4,
    [Description("AFTER_DOC_COMMITTED")] AfterDocCommitted = 5,
    [Description("BEFORE_DOC_REMOVED")] BeforeDocRemoved = 6,

    [Description("AFTER_DOC_REMOVED_PRE_RETRY")]
    AfterDocRemovedPreRetry = 7,

    [Description("AFTER_DOC_REMOVED_POST_RETRY")]
    AfterDocRemovedPostRetry = 8,
    [Description("AFTER_DOCS_REMOVED")] AfterDocsRemoved = 9,
    [Description("BEFORE_ATR_PENDING")] BeforeAtrPending = 10,
    [Description("AFTER_ATR_COMPLETE")] AfterAtrComplete = 11,

    [Description("BEFORE_ATR_ROLLED_BACK")]
    BeforeAtrRolledBack = 12,
    [Description("AFTER_GET_COMPLETE")] AfterGetComplete = 13,

    [Description("BEFORE_ROLLBACK_DELETE_INSERTED")]
    BeforeRollbackDeleteInserted = 15,

    [Description("AFTER_STAGED_REPLACE_COMPLETE")]
    AfterStagedReplaceComplete = 16,

    [Description("AFTER_STAGED_REMOVE_COMPLETE")]
    AfterStagedRemoveComplete = 17,
    [Description("BEFORE_STAGED_INSERT")] BeforeStagedInsert = 18,
    [Description("BEFORE_STAGED_REMOVE")] BeforeStagedRemove = 19,
    [Description("BEFORE_STAGED_REPLACE")] BeforeStagedReplace = 20,

    [Description("AFTER_STAGED_INSERT_COMPLETE")]
    AfterStagedInsertComplete = 21,

    [Description("BEFORE_GET_ATR_FOR_ABORT")]
    BeforeGetAtrForAbort = 22,
    [Description("BEFORE_ATR_ABORTED")] BeforeAtrAborted = 23,
    [Description("AFTER_ATR_ABORTED")] AfterAtrAborted = 24,
    [Description("AFTER_ATR_ROLLED_BACK")] AfterAtrRolledBack = 25,

    [Description("AFTER_ROLLBACK_REPLACE_OR_REMOVE")]
    AfterRollbackReplaceOrRemove = 26,

    [Description("AFTER_ROLLBACK_DELETE_INSERTED")]
    AfterRollbackDeleteInserted = 27,

    [Description("BEFORE_REMOVING_DOC_DURING_STAGING_INSERT")]
    BeforeRemovingDocDuringStagingInsert = 28,

    [Description("BEFORE_CHECK_ATR_ENTRY_FOR_BLOCKING_DOC")]
    BeforeCheckAtrEntryForBlockingDoc = 29,
    [Description("BEFORE_DOC_GET")] BeforeDocGet = 30,

    [Description("BEFORE_GET_DOC_IN_EXISTS_DURING_STAGED_INSERT")]
    BeforeGetDocInExistsDuringStagedInsert = 31,
    [Description("AFTER_ATR_PENDING")] AfterAtrPending = 32,
    [Description("BEFORE_ATR_COMPLETE")] BeforeAtrComplete = 33,

    [Description("BEFORE_ATR_COMMIT_AMBIGUITY_RESOLUTION")]
    BeforeAtrCommitAmbiguityResolution = 36,
    [Description("BEFORE_QUERY")] BeforeQuery = 37,

    [Description("BEFORE_REMOVE_STAGED_INSERT")]
    BeforeRemoveStagedInsert = 38,

    [Description("AFTER_REMOVE_STAGED_INSERT")]
    AfterRemoveStagedInsert = 39,
    [Description("AFTER_QUERY")] AfterQuery = 40,

    [Description("BEFORE_DOC_CHANGED_DURING_COMMIT")]
    BeforeDocChangedDuringCommit = 41,

    /// <summary>
    /// The BEFORE_UNLOCK_ hooks are deprecated, removed from ExtThreadSafety, and FIT will not send them.  Performers
    /// do not need to implement them, and can remove them.
    /// </summary>
    [Description("BEFORE_UNLOCK_GET")] BeforeUnlockGet = 42,
    [Description("BEFORE_UNLOCK_INSERT")] BeforeUnlockInsert = 43,
    [Description("BEFORE_UNLOCK_REPLACE")] BeforeUnlockReplace = 44,
    [Description("BEFORE_UNLOCK_REMOVE")] BeforeUnlockRemove = 45,
    [Description("BEFORE_UNLOCK_QUERY")] BeforeUnlockQuery = 46,

    [Description("BEFORE_DOC_CHANGED_DURING_ROLLBACK")]
    BeforeDocChangedDuringRollback = 47,

    [Description("BEFORE_DOC_CHANGED_DURING_STAGING")]
    BeforeDocChangedDuringStaging = 48,

    /// <summary>
    /// Injects that the transaction has expired at a certain point
    /// Does not take a HookAction
    /// hookConditionParam2 determines the hook point
    /// </summary>
    [Description("HAS_EXPIRED")] HasExpired = 34,

    /// <summary>
    /// Overrides the ATR id chosen for a given vbucket
    /// </summary>
    [Description("ATR_ID_FOR_VBUCKET")] AtrIdForVbucket = 35,

    /// <summary>
    /// Cleanup hooks
    /// </summary>
    [Description("CLEANUP_BEFORE_COMMIT_DOC")]
    CleanupBeforeCommitDoc = 101,

    [Description("CLEANUP_BEFORE_REMOVE_DOC_STAGED_FOR_REMOVAL")]
    CleanupBeforeRemoveDocStagedForRemoval = 102,

    [Description("CLEANUP_BEFORE_DOC_GET")]
    CleanupBeforeDocGet = 103,

    [Description("CLEANUP_BEFORE_REMOVE_DOC")]
    CleanupBeforeRemoveDoc = 104,

    [Description("CLEANUP_BEFORE_REMOVE_DOC_LINKS")]
    CleanupBeforeRemoveDocLinks = 105,

    [Description("CLEANUP_BEFORE_ATR_REMOVE")]
    CleanupBeforeAtrRemove = 106,
    [Description("CLEANUP_MARKER_LAST")] CleanupMarkerLast = 199,

    /// <summary>
    /// Client record hooks
    /// </summary>
    [Description("CLIENT_RECORD_BEFORE_UPDATE_CAS")]
    ClientRecordBeforeUpdateCas = 201,

    [Description("CLIENT_RECORD_BEFORE_CREATE")]
    ClientRecordBeforeCreate = 202,

    [Description("CLIENT_RECORD_BEFORE_GET")]
    ClientRecordBeforeGet = 203,

    [Description("CLIENT_RECORD_BEFORE_UPDATE")]
    ClientRecordBeforeUpdate = 204,

    [Description("CLIENT_RECORD_BEFORE_REMOVE_CLIENT")]
    ClientRecordBeforeRemoveClient = 205,

    [Description("CLIENT_RECORD_MARKER_LAST")]
    ClientRecordMarkerLast = 299,
}
