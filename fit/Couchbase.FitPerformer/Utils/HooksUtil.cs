#nullable enable
using Couchbase.Grpc.Protocol.Hooks.Transactions;
using System;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.Exceptions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Couchbase.Client.Transactions.Internal.Test;
using Couchbase.Client.Transactions.Error.Internal;
using ErrorClass = Couchbase.Client.Transactions.Error.ErrorClass;
using Couchbase.Client.Transactions;
using Newtonsoft.Json.Linq;

namespace Couchbase.FitPerformer.Utils
{
    public class HooksUtil
    {
        public static void ConfigureHooks(Google.Protobuf.Collections.RepeatedField<Hook> hooks, ClusterConnection connection, Transactions transactions)
        {
            var testHooks = ConfigureHooks(hooks, connection);
            transactions.ConfigureTestHooks(testHooks, testHooks);
        }
        public static GrpcAwareTestHooks ConfigureHooks(Google.Protobuf.Collections.RepeatedField<Hook> hooks, ClusterConnection connection)
        {
            Serilog.Log.Debug("Registering {HooksCount} hooks", hooks.Count);
            foreach (var hook in hooks)
            {
                Serilog.Log.Debug("  - Hook Point {Point} with Action {Action}", hook.HookPoint, hook.HookAction);
            }

            var result = new GrpcAwareTestHooks(hooks, connection);
            var usedHookPoints = hooks.Select(h => h.HookPoint).ToHashSet();
            var implementedHookPoints = result.GetType().GetMethods()
                .SelectMany(m => m.GetCustomAttributes(inherit: true))
                .Where(attr => attr is HookPointAttribute)
                .Select(attr => ((HookPointAttribute)attr).HookPoint)
                .ToHashSet();
            var unimplementedHooks = usedHookPoints.Except(implementedHookPoints).OrderBy(hp => hp.ToString()).ToList();
            if (unimplementedHooks.Count > 0)
            {
                var msg = "Unimplemented Test Hooks: " + string.Join(", ", unimplementedHooks);
                Serilog.Log.Error(msg);
                throw new NotImplementedException(msg);
            }

            return result;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class HookPointAttribute : Attribute
    {
        public HookPointAttribute(HookPoint hookPoint)
        {
            HookPoint = hookPoint;
        }

        public HookPoint HookPoint { get; set; }
    }

    public class GrpcAwareTestHooks : ITestHooks, ICleanupTestHooks
    {
        private const int HAS_EXPIRED_SENTINEL = -255;
        private readonly ICollection<Hook> _hooks;
        private readonly ClusterConnection _connection;
        private readonly CallCounts _callCounts;

        // ReSharper disable once IdentifierTypo
        public GrpcAwareTestHooks(ICollection<Hook> hooks, ClusterConnection connection)
        {
            _hooks = hooks;
            _connection = connection;
            _callCounts = new CallCounts();
        }

        [HookPoint(HookPoint.BeforeAtrCommit)]
        public async Task<int?> BeforeAtrCommit(AttemptContext ctx) => await SelectHook(HookPoint.BeforeAtrCommit, ctx, null, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.AfterAtrCommit)]
        public async Task<int?> AfterAtrCommit(AttemptContext ctx) => await SelectHook(HookPoint.AfterAtrCommit, ctx, null, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.BeforeDocCommitted)]
        public async Task<int?> BeforeDocCommitted(AttemptContext ctx, string id) => await SelectHook(HookPoint.BeforeDocCommitted, ctx, id, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.BeforeDocRolledBack)]
        public async Task<int?> BeforeDocRolledBack(AttemptContext ctx, string id) => await SelectHook(HookPoint.BeforeDocRolledBack, ctx, id, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.AfterDocCommittedBeforeSavingCas)]
        public async Task<int?> AfterDocCommittedBeforeSavingCas(AttemptContext ctx, string id) => await SelectHook(HookPoint.AfterDocCommittedBeforeSavingCas, ctx, id, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.BeforeDocChangedDuringCommit)]
        public async Task<int?> BeforeDocChangedDuringCommit(AttemptContext ctx, string id) => await SelectHook(HookPoint.BeforeDocChangedDuringCommit, ctx, id, 0).ConfigureAwait(false);
        [HookPoint(HookPoint.AfterDocCommitted)]
        public async Task<int?> AfterDocCommitted(AttemptContext ctx, string id) => await SelectHook(HookPoint.AfterDocCommitted, ctx, id, 0).ConfigureAwait(false);

        public Task<int?> AfterDocsCommitted(AttemptContext self)
        {
            throw new NotImplementedException();
        }

        [HookPoint(HookPoint.BeforeDocRemoved)]
        public async Task<int?> BeforeDocRemoved(AttemptContext ctx, string id) => await SelectHook(HookPoint.BeforeDocRemoved, ctx, id, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.AfterDocRemovedPreRetry)]
        public async Task<int?> AfterDocRemovedPreRetry(AttemptContext ctx, string id) => await SelectHook(HookPoint.AfterDocRemovedPreRetry, ctx, id, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.AfterDocRemovedPostRetry)]
        public async Task<int?> AfterDocRemovedPostRetry(AttemptContext ctx, string id) => await SelectHook(HookPoint.AfterDocRemovedPostRetry, ctx, id, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.AfterDocsRemoved)]
        public async Task<int?> AfterDocsRemoved(AttemptContext ctx) => await SelectHook(HookPoint.AfterDocsRemoved, ctx, null, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.BeforeAtrPending)]
        public async Task<int?> BeforeAtrPending(AttemptContext ctx) => await SelectHook(HookPoint.BeforeAtrPending, ctx, null, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.AfterAtrPending)]
        public async Task<int?> AfterAtrPending(AttemptContext ctx) => await SelectHook(HookPoint.AfterAtrPending, ctx, null, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.AfterAtrComplete)]
        public async Task<int?> AfterAtrComplete(AttemptContext ctx) => await SelectHook(HookPoint.AfterAtrComplete, ctx, null, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.BeforeAtrComplete)]
        public async Task<int?> BeforeAtrComplete(AttemptContext ctx) => await SelectHook(HookPoint.BeforeAtrComplete, ctx, null, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.BeforeAtrRolledBack)]
        public async Task<int?> BeforeAtrRolledBack(AttemptContext ctx) => await SelectHook(HookPoint.BeforeAtrRolledBack, ctx, null, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.AfterGetComplete)]
        public async Task<int?> AfterGetComplete(AttemptContext ctx, string id) => await SelectHook(HookPoint.AfterGetComplete, ctx, id, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.BeforeRollbackDeleteInserted)]
        public async Task<int?> BeforeRollbackDeleteInserted(AttemptContext ctx, string id) => await SelectHook(HookPoint.BeforeRollbackDeleteInserted, ctx, id, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.AfterStagedReplaceComplete)]
        public async Task<int?> AfterStagedReplaceComplete(AttemptContext ctx, string id) => await SelectHook(HookPoint.AfterStagedReplaceComplete, ctx, id, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.AfterStagedRemoveComplete)]
        public async Task<int?> AfterStagedRemoveComplete(AttemptContext ctx, string id) => await SelectHook(HookPoint.AfterStagedRemoveComplete, ctx, id, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.BeforeStagedInsert)]
        public async Task<int?> BeforeStagedInsert(AttemptContext ctx, string id) => await SelectHook(HookPoint.BeforeStagedInsert, ctx, id, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.BeforeStagedRemove)]
        public async Task<int?> BeforeStagedRemove(AttemptContext ctx, string id) => await SelectHook(HookPoint.BeforeStagedRemove, ctx, id, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.BeforeStagedReplace)]
        public async Task<int?> BeforeStagedReplace(AttemptContext ctx, string id) => await SelectHook(HookPoint.BeforeStagedReplace, ctx, id, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.AfterStagedInsertComplete)]
        public async Task<int?> AfterStagedInsertComplete(AttemptContext ctx, string id) => await SelectHook(HookPoint.AfterStagedInsertComplete, ctx, id, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.BeforeGetAtrForAbort)]
        public async Task<int?> BeforeGetAtrForAbort(AttemptContext ctx) => await SelectHook(HookPoint.BeforeGetAtrForAbort, ctx, null, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.BeforeAtrAborted)]
        public async Task<int?> BeforeAtrAborted(AttemptContext ctx) => await SelectHook(HookPoint.BeforeAtrAborted, ctx, null, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.AfterAtrAborted)]
        public async Task<int?> AfterAtrAborted(AttemptContext ctx) => await SelectHook(HookPoint.AfterAtrAborted, ctx, null, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.AfterAtrRolledBack)]
        public async Task<int?> AfterAtrRolledBack(AttemptContext ctx) => await SelectHook(HookPoint.AfterAtrRolledBack, ctx, null, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.AfterRollbackReplaceOrRemove)]
        public async Task<int?> AfterRollbackReplaceOrRemove(AttemptContext ctx, string id) => await SelectHook(HookPoint.AfterRollbackReplaceOrRemove, ctx, id, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.AfterRollbackDeleteInserted)]
        public async Task<int?> AfterRollbackDeleteInserted(AttemptContext ctx, string id) => await SelectHook(HookPoint.AfterRollbackDeleteInserted, ctx, id, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.BeforeRemovingDocDuringStagingInsert)]
        public async Task<int?> BeforeRemovingDocDuringStagedInsert(AttemptContext ctx, string id) => await SelectHook(HookPoint.BeforeRemovingDocDuringStagingInsert, ctx, id, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.BeforeCheckAtrEntryForBlockingDoc)]
        public async Task<int?> BeforeCheckAtrEntryForBlockingDoc(AttemptContext ctx, string id) => await SelectHook(HookPoint.BeforeCheckAtrEntryForBlockingDoc, ctx, id, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.BeforeDocGet)]
        public async Task<int?> BeforeDocGet(AttemptContext ctx, string id) => await SelectHook(HookPoint.BeforeDocGet, ctx, id, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.BeforeGetDocInExistsDuringStagedInsert)]
        public async Task<int?> BeforeGetDocInExistsDuringStagedInsert(AttemptContext ctx, string id) => await SelectHook(HookPoint.BeforeGetDocInExistsDuringStagedInsert, ctx, id, 0).ConfigureAwait(false);

        [HookPoint(HookPoint.HasExpired)]
        public bool HasExpiredClientSideHook(AttemptContext ctx, string place, string docId)
        {
            var result = SelectHookRaw(HookPoint.HasExpired, ctx, place, false, docId).Result;
            if (result is bool boolResult)
            {
                return boolResult;
            }
            else
            {
                Serilog.Log.Error("HasExpiredClientSideHook did not return a bool: {Actual}", result);
                return false;
            }
        }

        [HookPoint(HookPoint.BeforeAtrCommitAmbiguityResolution)]
        public async Task<int?> BeforeAtrCommitAmbiguityResolution(AttemptContext ctx) => await SelectHook(HookPoint.BeforeAtrCommitAmbiguityResolution, ctx, null, 0);

        [HookPoint(HookPoint.AtrIdForVbucket)]
        public async Task<string?> AtrIdForVBucket(AttemptContext ctx, int vbucketId) => (string?)await SelectHookRaw(HookPoint.AtrIdForVbucket, ctx, vbucketId.ToString(), null, null);

        [HookPoint(HookPoint.AfterQuery)]
        public async Task<int?> AfterQuery(AttemptContext ctx, string query) => await SelectHook(HookPoint.AfterQuery, ctx, query, null, null);

        #region Cleanup Hooks
        [HookPoint(HookPoint.ClientRecordBeforeGet)]
        public Task<int?> BeforeGetRecord(string clientUuid) => SelectHook(HookPoint.ClientRecordBeforeGet, null, clientUuid, 0, clientUuid);
        [HookPoint(HookPoint.ClientRecordBeforeUpdate)]
        public Task<int?> BeforeUpdateRecord(string clientUuid) => SelectHook(HookPoint.ClientRecordBeforeUpdate, null, clientUuid, 0, clientUuid);
        [HookPoint(HookPoint.ClientRecordBeforeCreate)]
        public Task<int?> BeforeCreateRecord(string clientUuid) => SelectHook(HookPoint.ClientRecordBeforeCreate, null, clientUuid, 0, clientUuid);
        [HookPoint(HookPoint.ClientRecordBeforeRemoveClient)]
        public Task<int?> BeforeRemoveClient(string clientUuid) => SelectHook(HookPoint.ClientRecordBeforeRemoveClient, null, clientUuid, 0, clientUuid);
        [HookPoint(HookPoint.CleanupBeforeAtrRemove)]
        public Task<int?> BeforeAtrRemove(string id) => SelectHook(HookPoint.CleanupBeforeAtrRemove, null, id, 0, id);
        [HookPoint(HookPoint.CleanupBeforeRemoveDoc)]
        public Task<int?> BeforeRemoveDoc(string id) => SelectHook(HookPoint.CleanupBeforeRemoveDoc, null, id, 0, id);
        [HookPoint(HookPoint.CleanupBeforeRemoveDocStagedForRemoval)]
        public Task<int?> BeforeRemoveDocStagedForRemoval(string id) => SelectHook(HookPoint.CleanupBeforeRemoveDocStagedForRemoval, null, id, 0, id);
        [HookPoint(HookPoint.CleanupBeforeRemoveDocLinks)]
        public Task<int?> BeforeRemoveLinks(string id) => SelectHook(HookPoint.CleanupBeforeRemoveDocLinks, null, id, 0, id);
        [HookPoint(HookPoint.CleanupBeforeCommitDoc)]
        public Task<int?> BeforeCommitDoc(string id) => SelectHook(HookPoint.CleanupBeforeCommitDoc, null, id, 0, id);

        [HookPoint(HookPoint.BeforeQuery)]
        public Task<int?> BeforeQuery(AttemptContext ctx, string statement) => SelectHook(HookPoint.BeforeQuery, ctx, statement, 0, null);
        // AFTER_QUERY doesn't seem to be used?

        [HookPoint(HookPoint.BeforeRemoveStagedInsert)]
        public Task<int?> BeforeRemoveStagedInsert(AttemptContext ctx, string id) => SelectHook(HookPoint.BeforeRemoveStagedInsert, ctx, id, 0, null);

        [HookPoint(HookPoint.AfterRemoveStagedInsert)]
        public Task<int?> AfterRemoveStagedInsert(AttemptContext ctx, string id) => SelectHook(HookPoint.AfterRemoveStagedInsert, ctx, id, 0, null);

        [HookPoint(HookPoint.BeforeDocGet)]
         public Task<int?> BeforeDocGet(string id) => SelectHook(HookPoint.BeforeDocGet, null, id, 0, null);
         // TODO: why is this in the interface with no corresponding HookPoint?
         public Task <int?> BeforeAtrGet(string id) => DefaultCleanupTestHooks.Instance.BeforeAtrGet(id);

        [HookPoint(HookPoint.BeforeRemovingDocDuringStagingInsert)]
        public Task <int?> BeforeRemovingDocDuringStagedInsert(AttemptContext ctx) => SelectHook(HookPoint.BeforeRemovingDocDuringStagingInsert, ctx, null, 0, null);

        // TODO: why no HookPoint for this too?
        public Task<int?> BeforeOverwritingStagedInsertRemoval(AttemptContext ctx, string id) =>
            DefaultTestHooks.Instance.BeforeOverwritingStagedInsertRemoval(ctx, id);

        #endregion
        private async Task<int?> SelectHook(HookPoint hp, AttemptContext ctx, string param, int? def, string docId = null) => (int?)await SelectHookRaw(hp, ctx, param, def, docId);
        private async Task<object?> SelectHookRaw(HookPoint hp, AttemptContext ctx, string param, object? def, string docId = null)
        {
            if (_hooks.Count == 0)
            {
                return def;
            }

            try
            {
                return await _hooks
                    .Where(h => h.HookPoint == hp)
                    .Select(async h => await ConfigureHook(ctx, h, param, docId).ConfigureAwait(false))
                    .First()!.ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                // We just return the default if no hook was found
                return def;
            }
        }

        private async Task<object?> ConfigureHook(AttemptContext ctx, Hook hook, string param, string docId)
        {
            // TODO: this whole method is a mess, leftover from a 1-1 reimplementation of the java version.  It needs to be refactored.
            var defaultResult = hook.HookPoint == HookPoint.HasExpired
                ? Task.FromResult<object?>(false)
                : Task.FromResult<object?>(1);
            var t = Task.Run(async () =>
            {
                Func<Task<object?>> action;
                Task<object?> result;

                _callCounts.Add(hook.HookPoint);
                switch (hook.HookAction)
                {
                    case HookAction.FailHard:
                        action = () => throw new TestFailHardException();
                        break;
                    case HookAction.FailAmbiguous:
                        action = () => throw new TestFailAmbiguousException();
                        break;
                    case HookAction.FailOther:
                        action = () => throw new TestFailOtherException();
                        break;
                    case HookAction.FailTransient:
                        action = () => throw new TestFailTransientException();
                        break;
                    case HookAction.FailDocNotFound:
                        action = () => throw new DocumentNotFoundException();
                        break;
                    case HookAction.FailDocAlreadyExists:
                        action = () => throw new DocumentExistsException();
                        break;
                    case HookAction.FailPathAlreadyExists:
                        action = () => throw new PathExistsException();
                        break;
                    case HookAction.FailPathNotFound:
                        action = () => throw new PathNotFoundException();
                        break;
                    case HookAction.FailCasMismatch:
                        action = () => throw new CasMismatchException();
                        break;
                    case HookAction.FailAtrFull:
                        action = () => throw new ValueToolargeException();
                        break;
                    case HookAction.ReturnString:
                        // ReSharper disable once StringLiteralTypo
                        action = () => Task.FromResult<object?>(hook.HookActionParam1);
                        break;
                    case HookAction.MutateDoc:
                    case HookAction.RemoveDoc:
                        var docLocation = hook.HookActionParam1;
                        var content = hook.HookActionParam2;

                        var splits = docLocation.Split("/");
                        var bucketName = splits[0];
                        var collectionName = splits[1];
                        var docId = splits[2];

                        var collection = await _connection.GetCollectionAsync(bucketName, "_default", collectionName).ConfigureAwait(false);

                        if (hook.HookAction == HookAction.MutateDoc)
                        {
                            action =async () =>
                            {
                                var upsertResult = await collection.UpsertAsync(docId, JObject.Parse(content)).ConfigureAwait(false);
                                Serilog.Log.Debug("Mutated via hook, new Cas = {Cas}", upsertResult.Cas);
                                return (object?)0;
                            };
                        }
                        else
                        {
                            action = async () =>
                            {
                                await collection.RemoveAsync(docId).ConfigureAwait(false);
                                return (object?)0;
                            };
                        }

                        break;
                    case HookAction.Block:
                    {
                        action = async () =>
                        {
                            var delayMs = int.Parse(hook.HookActionParam1);
                            Serilog.Log.Debug("Blocking via hook, delay = {DelayMs}", delayMs);
                            await Task.Delay(TimeSpan.FromMilliseconds(delayMs));
                            return (object?)0;
                        };
                        break;
                    }
                    default:
                        throw new System.Exception("Unexpected HookAction " + hook.HookAction);
                }

                switch (hook.HookCondition)
                {
                    case HookCondition.OnCall:
                        {
                            var desiredCallNumber = hook.HookConditionParam1;
                            var callNumber = _callCounts.GetCount(hook.HookPoint);
                            result = callNumber == desiredCallNumber ? action() : defaultResult;
                            break;
                        }
                    case HookCondition.OnCallLe:
                        {
                            var desiredCallNumber = hook.HookConditionParam1;
                            var callNumber = _callCounts.GetCount(hook.HookPoint);
                            result = callNumber <= desiredCallNumber ? action() : defaultResult;
                            break;
                        }
                    case HookCondition.OnCallGe:
                        {
                            var desiredCallNumber = hook.HookConditionParam1;
                            var callNumber = _callCounts.GetCount(hook.HookPoint);
                            result = callNumber >= desiredCallNumber ? action() : defaultResult;
                            break;
                        }
                    case HookCondition.Always:
                        result = action();
                        break;
                    case HookCondition.Equals:
                        {
                            var desiredParam = hook.HookConditionParam2;
                            if (hook.HookPoint == HookPoint.HasExpired)
                            {
                                result = Task.FromResult<object?>(desiredParam.Equals(param));
                            }
                            else
                            {
                                result = desiredParam.Equals(param) ? action() : defaultResult;
                            }
                            break;
                        }
                    case HookCondition.EqualsBoth:
                        {
                            if (hook.HookPoint != HookPoint.HasExpired)
                            {
                                throw new NotSupportedException($"{HookCondition.EqualsBoth} only supported on {HookPoint.HasExpired}");
                            }

                            bool? resultVal;
                            if (string.IsNullOrEmpty(docId))
                            {
                                // Cannot perform EQUALS_BOTH if docId not present
                                resultVal = false;
                            }
                            else
                            {
                                var stage = param;
                                var equalsBoth = stage == hook.HookConditionParam3 && docId == hook.HookConditionParam2;
                                resultVal = equalsBoth;
                            }

                            result = Task.FromResult<object?>(resultVal);
                            break;
                        }
                    case HookCondition.OnCallAndEquals:
                        {
                            _callCounts.Add(hook.HookPoint, param);

                            var desiredCallNumber = hook.HookConditionParam1;
                            var desiredParam = hook.HookConditionParam2;
                            var callNumber = _callCounts.GetCount(hook.HookPoint, param);

                            if (callNumber == desiredCallNumber && desiredParam.Equals(param))
                            {
                                result = action();
                            }
                            else
                            {
                                result = defaultResult;
                            }
                            break;
                        }
                    case HookCondition.WhileExpired:
                        {
                            var expired = ctx.HasExpiredClientSide(null, nameof(HookCondition.WhileExpired));
                            result = expired ? action() : defaultResult;
                            break;
                        }
                    case HookCondition.WhileNotExpired:
                        {
                            var expired = ctx.HasExpiredClientSide(null, nameof(HookCondition.WhileNotExpired));
                            result = !expired ? action() : defaultResult;
                            break;
                        }
                    default:
                        {
                            var msg = "Unexpected HookCondition " + hook.HookCondition;
                            try
                            {
                                var json = JObject.FromObject(hook).ToString();
                                msg += "\n" + json;
                            }
                            catch
                            { }

                            throw new System.Exception(msg);
                        }
                }

                var performedAction = await result.ConfigureAwait(false);
                return performedAction;
            });

            var finalResult = await t;
            return finalResult;
        }
    }

    public class TestFailHardException : System.Exception, IClassifiedTransactionError
    {
        public ErrorClass CausingErrorClass => ErrorClass.FailHard;
    }

    public class TestFailOtherException : System.Exception, IClassifiedTransactionError
    {
        public ErrorClass CausingErrorClass => ErrorClass.FailOther;
    }

    public class TestFailTransientException : System.Exception, IClassifiedTransactionError
    {
        public ErrorClass CausingErrorClass => ErrorClass.FailTransient;
    }

    public class TestFailAmbiguousException : System.Exception, IClassifiedTransactionError
    {
        public ErrorClass CausingErrorClass => ErrorClass.FailAmbiguous;
    }
}
