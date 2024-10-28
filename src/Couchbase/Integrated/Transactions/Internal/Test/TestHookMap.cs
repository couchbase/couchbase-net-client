#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Integrated.Transactions.Internal.Test;

internal class TestHookMap
{
    // the majority of hooks are not async, take AttemptContext and DocId, and return nothing.
    // some hooks execute async operations, and need to be treated separately.
    private readonly Dictionary<HookPoint, Func<AttemptContext?, HookArgs?, object?>> _hookActionsSync = new();
    private readonly Dictionary<HookPoint, Func<AttemptContext?, HookArgs?, Task<object?>>> _hookActionsAsync = new();

    public void SetHookSync(HookPoint hookPoint, Action<AttemptContext?, HookArgs?> action)
    {
        var func = (AttemptContext? ctx, HookArgs? args) =>
        {
            action(ctx, args);
            return (object?)null;
        };

        SetHookSync(hookPoint, func);
    }

    public void SetHookSync(HookPoint hookPoint, Func<AttemptContext?, HookArgs?, object?> func)
    {
        _hookActionsSync[hookPoint] = func;
    }

    public void SetHookAsync(HookPoint hookPoint, Func<AttemptContext?, HookArgs?, Task<object?>> func)
    {
        _hookActionsAsync[hookPoint] = func;
    }

    public object? Sync(HookPoint hookPoint, AttemptContext? ctx = null, HookArgs? args = null)
    {
        if (_hookActionsSync.TryGetValue(hookPoint, out var func))
        {
            return func(ctx, args);
        }

        return null;
    }

    public async Task<object?> Async(HookPoint hookPoint, AttemptContext? ctx, HookArgs args)
    {
        if (_hookActionsAsync.TryGetValue(hookPoint, out var func))
        {
            return await func(ctx, args).CAF();
        }

        return null;
    }
}

public record HookArgs(string? hookParam1, string? hookParam2 = null)
{
    public static implicit operator HookArgs(string? docId) => new(docId);
};

public static class DefaultTestHooks
{
    // ReSharper disable InconsistentNaming
    // naming CONSTANT_STYLE to make finding in the spec easier
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
    // ReSharper restore InconsistentNaming
}
