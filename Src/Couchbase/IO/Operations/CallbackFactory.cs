using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Configuration;
using Couchbase.Configuration.Server.Providers.FileSystem;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Utils;

namespace Couchbase.IO.Operations
{
    internal static class CallbackFactory
    {
        public static Func<SocketAsyncState, Task> CompletedFuncWithRetryForMemcached<T>(IRequestExecuter executer,
            ConcurrentDictionary<uint, IOperation> pending, IClusterController controller,
            TaskCompletionSource<IOperationResult<T>> tcs, CancellationToken cancellationToken)
        {
            Func<SocketAsyncState, Task> func = async s =>
            {
                IOperation op;
                if (pending.TryRemove(s.Opaque, out op))
                {
                    var actual = (IOperation<T>)op;
                    try
                    {
                        var response = s.Data.ToArray();
                        await op.ReadAsync(response, 0, response.Length)
                            .ContinueOnAnyContext();

                        var result = actual.GetResult();
                        if (result.Success)
                        {
                            tcs.SetResult(result);
                        }
                        else
                        {
                            if (result.Status == ResponseStatus.VBucketBelongsToAnotherServer)
                            {
                                var config = actual.GetConfig();
                                if (config != null)
                                {
                                    controller.NotifyConfigPublished(config);
                                }
                            }
                            if (op.CanRetry() && result.ShouldRetry())
                            {
                                var retryResult = await executer.RetryOperationEveryAsync((o, c) =>
                                {
                                    var retryTcs = new TaskCompletionSource<IOperationResult<T>>();

                                    var cloned = o.Clone();
                                    cloned.Completed = CompletedFuncForRetry(executer, pending, controller, retryTcs);
                                    pending.TryAdd(cloned.Opaque, cloned);

                                    var keyMapper = c.GetKeyMapper();
                                    var mappedNode= keyMapper.MapKey(cloned.Key);
                                    var server = mappedNode.LocatePrimary();
                                    server.SendAsync(o).ContinueOnAnyContext();

                                    return retryTcs.Task;
                                }, actual, executer.ConfigInfo, cancellationToken).ContinueOnAnyContext();
                                tcs.SetResult(retryResult);
                            }
                            else
                            {
                                tcs.SetResult(result);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        op.Exception = e;
                        op.HandleClientError(e.Message, ResponseStatus.ClientFailure);
                        tcs.SetResult(actual.GetResult());
                    }
                }
                else
                {
                    const string msg = "Cannot find callback object for operation: {0}";
                    tcs.SetException(new InvalidOperationException(string.Format(msg, s.Opaque)));
                }
            };
            return func;
        }

        public static Func<SocketAsyncState, Task> CompletedFuncWithRetryForCouchbase<T>(IRequestExecuter executer,
            ConcurrentDictionary<uint, IOperation> pending, IClusterController controller,
            TaskCompletionSource<IOperationResult<T>> tcs, CancellationToken cancellationToken)
        {
            Func<SocketAsyncState, Task> func = async s =>
            {
                IOperation op;
                if (pending.TryRemove(s.Opaque, out op))
                {
                    var actual = (IOperation<T>)op;
                    try
                    {
                        var response = s.Data.ToArray();
                        await op.ReadAsync(response, 0, response.Length)
                            .ContinueOnAnyContext();

                        var result = actual.GetResult();
                        if (result.Success)
                        {
                            tcs.SetResult(result);
                        }
                        else
                        {
                            if (result.Status == ResponseStatus.VBucketBelongsToAnotherServer)
                            {
                                var config = actual.GetConfig();
                                if (config != null)
                                {
                                    controller.NotifyConfigPublished(config);
                                }
                            }
                            if (op.CanRetry() && result.ShouldRetry())
                            {
                                var retryResult = await executer.RetryOperationEveryAsync((o, c) =>
                                {
                                    var retryTcs = new TaskCompletionSource<IOperationResult<T>>();

                                    var cloned = o.Clone();
                                    cloned.Completed = CompletedFuncForRetry(executer, pending, controller, retryTcs);
                                    pending.TryAdd(cloned.Opaque, cloned);

                                    var keyMapper = c.GetKeyMapper();
                                    var vBucket = (IVBucket)keyMapper.MapKey(o.Key);
                                    o.VBucket = vBucket;

                                    var server = vBucket.LocatePrimary();
                                    server.SendAsync(o).ContinueOnAnyContext();

                                    return retryTcs.Task;
                                }, actual, executer.ConfigInfo, cancellationToken).ContinueOnAnyContext();
                                tcs.SetResult(retryResult);
                            }
                            else
                            {
                                tcs.SetResult(result);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        op.Exception = e;
                        op.HandleClientError(e.Message, ResponseStatus.ClientFailure);
                        tcs.SetResult(actual.GetResult());
                    }
                }
                else
                {
                    const string msg = "Cannot find callback object for operation: {0}";
                    tcs.SetException(new InvalidOperationException(string.Format(msg, s.Opaque)));
                }
            };
            return func;
        }

        public static Func<SocketAsyncState, Task> CompletedFuncForRetry<T>(IRequestExecuter executer,
          ConcurrentDictionary<uint, IOperation> pending, IClusterController controller,
          TaskCompletionSource<IOperationResult<T>> tcs)
        {
            Func<SocketAsyncState, Task> func = async s =>
            {
                IOperation op;
                if (pending.TryRemove(s.Opaque, out op))
                {
                    var actual = (IOperation<T>)op;
                    try
                    {
                        var response = s.Data.ToArray();
                        await op.ReadAsync(response, 0, response.Length)
                            .ContinueOnAnyContext();

                        var result = actual.GetResult();
                        if (result.Status == ResponseStatus.VBucketBelongsToAnotherServer)
                        {
                            var config = actual.GetConfig();
                            if (config != null)
                            {
                                controller.NotifyConfigPublished(config);
                            }
                        }
                       tcs.SetResult(result);
                    }
                    catch (Exception e)
                    {
                        op.Exception = e;
                        op.HandleClientError(e.Message, ResponseStatus.ClientFailure);
                        tcs.SetResult(actual.GetResult());
                    }
                }
                else
                {
                    const string msg = "Cannot find callback object for operation: {0}";
                    tcs.SetException(new InvalidOperationException(string.Format(msg, s.Opaque)));
                }
            };
            return func;
        }
    }
}
