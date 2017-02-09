using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Logging;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.Utils;

namespace Couchbase.Core.Buckets
{
    internal static class CallbackFactory
    {
        private readonly static ILog Log = LogManager.GetLogger(typeof(CouchbaseRequestExecuter));

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
                        //check if an error occurred earlier
                        if (s.Exception != null)
                        {
                            actual.Exception = s.Exception;
                            actual.HandleClientError(s.Exception.Message, s.Status);
                            tcs.SetResult(actual.GetResultWithValue());
                            return;
                        }

                        var response = s.Data.ToArray();
                        await op.ReadAsync(response, 0, response.Length)
                            .ContinueOnAnyContext();

                        var result = actual.GetResultWithValue();
                        if (result.Success)
                        {
                            tcs.SetResult(result);
                        }
                        else
                        {
                            if (result.IsNmv())
                            {
                                var config = op.GetConfig();
                                if (config != null)
                                {
                                    controller.NotifyConfigPublished(config);
                                }
                            }
                            if (result.IsNmv() || (op.CanRetry() && result.ShouldRetry()))
                            {
                                var retryResult = await executer.RetryOperationEveryAsync((o, c) =>
                                {
                                    var retryTcs = new TaskCompletionSource<IOperationResult<T>>();

                                    var cloned = o.Clone();
                                    cloned.Completed = CompletedFuncForRetry(pending, controller, retryTcs);
                                    pending.TryAdd(cloned.Opaque, cloned);

                                    var keyMapper = c.GetKeyMapper();
                                    var mappedNode = keyMapper.MapKey(cloned.Key);

                                    IServer server;
                                    var attempts = 0;
                                    while ((server = mappedNode.LocatePrimary()) == null)
                                    {
                                        if (attempts++ > 10) { throw new TimeoutException("Could not acquire a server."); }
                                        Thread.Sleep((int)Math.Pow(2, attempts));
                                    }
                                    server.SendAsync(o).ContinueOnAnyContext();

                                    return retryTcs.Task;
                                }, actual, executer.ConfigInfo, cancellationToken).ContinueOnAnyContext();
                                tcs.SetResult(retryResult);
                            }
                            else
                            {
                                ((OperationResult)result).SetException();
                                tcs.SetResult(result);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        op.Exception = e;
                        op.HandleClientError(e.Message, ResponseStatus.ClientFailure);
                        tcs.SetResult(actual.GetResultWithValue());
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

        public static Func<SocketAsyncState, Task> CompletedFuncWithRetryForMemcached(IRequestExecuter executer,
            ConcurrentDictionary<uint, IOperation> pending, IClusterController controller,
            TaskCompletionSource<IOperationResult> tcs, CancellationToken cancellationToken)
        {
            Func<SocketAsyncState, Task> func = async s =>
            {
                IOperation op;
                if (pending.TryRemove(s.Opaque, out op))
                {
                    try
                    {
                        //check if an error occurred earlier
                        if (s.Exception != null)
                        {
                            op.Exception = s.Exception;
                            op.HandleClientError(s.Exception.Message, s.Status);
                            tcs.SetResult(op.GetResult());
                            return;
                        }

                        var response = s.Data.ToArray();
                        await op.ReadAsync(response, 0, response.Length)
                            .ContinueOnAnyContext();

                        var result = op.GetResult();
                        if (result.Success)
                        {
                            tcs.SetResult(result);
                        }
                        else
                        {
                            if (result.IsNmv())
                            {
                                var config = op.GetConfig();
                                if (config != null)
                                {
                                    controller.NotifyConfigPublished(config);
                                }
                            }
                            if (result.IsNmv() || (op.CanRetry() && result.ShouldRetry()))
                            {
                                Log.Trace("Retry {0} on {1}: {2}", op.Opaque,op.CurrentHost, result.Status);
                                var retryResult = await executer.RetryOperationEveryAsync((o, c) =>
                                {
                                    var retryTcs = new TaskCompletionSource<IOperationResult>();

                                    var cloned = o.Clone();
                                    cloned.Completed = CompletedFuncForRetry(pending, controller, retryTcs);
                                    pending.TryAdd(cloned.Opaque, cloned);

                                    var keyMapper = c.GetKeyMapper();
                                    var mappedNode = keyMapper.MapKey(cloned.Key);

                                    IServer server;
                                    var attempts = 0;
                                    while ((server = mappedNode.LocatePrimary()) == null)
                                    {
                                        if (attempts++ > 10) { throw new TimeoutException("Could not acquire a server."); }
                                        Thread.Sleep((int)Math.Pow(2, attempts));
                                    }
                                    server.SendAsync(o).ContinueOnAnyContext();

                                    return retryTcs.Task;
                                }, op, executer.ConfigInfo, cancellationToken).ContinueOnAnyContext();
                                tcs.SetResult(retryResult);
                            }
                            else
                            {
                                ((OperationResult)result).SetException();
                                tcs.SetResult(result);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        op.Exception = e;
                        op.HandleClientError(e.Message, ResponseStatus.ClientFailure);
                        tcs.SetResult(op.GetResult());
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

        public static Func<SocketAsyncState, Task> CompletedFuncWithRetryForCouchbase<T>(
            IRequestExecuter executer,
            ConcurrentDictionary<uint, IOperation> pending,
            IClusterController controller,
            TaskCompletionSource<IOperationResult<T>> tcs,
            CancellationToken cancellationToken)
        {
            Func<SocketAsyncState, Task> func = async s =>
            {
                IOperation op;
                if (pending.TryRemove(s.Opaque, out op))
                {
                    var actual = (IOperation<T>)op;
                    try
                    {
                        //check if an error occurred earlier
                        if (s.Exception != null)
                        {
                            actual.Exception = s.Exception;
                            actual.HandleClientError(s.Exception.Message, s.Status);
                            tcs.SetResult(actual.GetResultWithValue());
                            return;
                        }

                        var response = s.Data.ToArray();
                        await op.ReadAsync(response, 0, response.Length)
                            .ContinueOnAnyContext();

                        var result = actual.GetResultWithValue();
                        if (result.Success)
                        {
                            tcs.SetResult(result);
                        }
                        else
                        {
                            if (result.IsNmv())
                            {
                                var config = op.GetConfig();
                                if (config != null)
                                {
                                    controller.NotifyConfigPublished(config);
                                }
                            }
                            if (result.IsNmv() || (op.CanRetry() && result.ShouldRetry()))
                            {
                                Log.Trace("Retry {0} on {1}: {2}", op.Opaque, op.CurrentHost, result.Status);
                                var retryResult = await executer.RetryOperationEveryAsync((o, c) =>
                                {
                                    var retryTcs = new TaskCompletionSource<IOperationResult<T>>();

                                    var cloned = o.Clone();
                                    cloned.Completed = CompletedFuncForRetry(pending, controller, retryTcs);
                                    pending.TryAdd(cloned.Opaque, cloned);

                                    var keyMapper = c.GetKeyMapper();
                                    var vBucket = (IVBucket)keyMapper.MapKey(cloned.Key, cloned.LastConfigRevisionTried);
                                    cloned.LastConfigRevisionTried = vBucket.Rev;
                                    cloned.VBucket = vBucket;

                                    IServer server;
                                    var attempts = 0;
                                    while ((server = vBucket.LocatePrimary()) == null)
                                    {
                                        if (attempts++ > 10) { throw new TimeoutException("Could not acquire a server."); }
                                        Thread.Sleep((int)Math.Pow(2, attempts));
                                    }
                                    server.SendAsync(o).ContinueOnAnyContext();

                                    return retryTcs.Task;
                                }, actual, executer.ConfigInfo, cancellationToken).ContinueOnAnyContext();
                                tcs.SetResult(retryResult);
                            }
                            else
                            {
                                ((OperationResult)result).SetException();
                                tcs.SetResult(result);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        op.Exception = e;
                        op.HandleClientError(e.Message, s.Status);
                        tcs.SetResult(actual.GetResultWithValue());
                    }
                }
                else
                {
                    const string msg = "Cannot find callback object for operation: {0}";
                    tcs.TrySetException(new InvalidOperationException(string.Format(msg, s.Opaque)));
                }
            };
            return func;
        }

        public static Func<SocketAsyncState, Task> CompletedFuncWithRetryForCouchbase(IRequestExecuter executer,
           ConcurrentDictionary<uint, IOperation> pending, IClusterController controller,
           TaskCompletionSource<IOperationResult> tcs, CancellationToken cancellationToken)
        {
            Func<SocketAsyncState, Task> func = async s =>
            {
                IOperation op;
                if (pending.TryRemove(s.Opaque, out op))
                {
                    try
                    {
                        //check if an error occurred earlier
                        if (s.Exception != null)
                        {
                            op.Exception = s.Exception;
                            op.HandleClientError(s.Exception.Message, s.Status);
                            tcs.SetResult(op.GetResult());
                            return;
                        }

                        var response = s.Data.ToArray();
                        await op.ReadAsync(response, 0, response.Length)
                            .ContinueOnAnyContext();

                        var result = op.GetResult();
                        if (result.Success)
                        {
                            tcs.SetResult(result);
                        }
                        else
                        {
                            if (result.IsNmv())
                            {
                                var config = op.GetConfig();
                                if (config != null)
                                {
                                    controller.NotifyConfigPublished(config);
                                }
                            }
                            if (result.IsNmv() || (op.CanRetry() && result.ShouldRetry()))
                            {
                                Log.Trace("Retry {0} on {1}: {2}", op.Opaque, op.CurrentHost, result.Status);
                                var retryResult = await executer.RetryOperationEveryAsync((o, c) =>
                                {
                                    var retryTcs = new TaskCompletionSource<IOperationResult>();

                                    var cloned = o.Clone();
                                    cloned.Completed = CompletedFuncForRetry(pending, controller, retryTcs);
                                    pending.TryAdd(cloned.Opaque, cloned);

                                    var keyMapper = c.GetKeyMapper();
                                    var vBucket = (IVBucket)keyMapper.MapKey(cloned.Key, cloned.LastConfigRevisionTried);
                                    cloned.LastConfigRevisionTried = vBucket.Rev;
                                    cloned.VBucket = vBucket;

                                    IServer server;
                                    var attempts = 0;
                                    while ((server = vBucket.LocatePrimary()) == null)
                                    {
                                        if (attempts++ > 10) { throw new TimeoutException("Could not acquire a server."); }
                                        Thread.Sleep((int)Math.Pow(2, attempts));
                                    }
                                    server.SendAsync(o).ContinueOnAnyContext();

                                    return retryTcs.Task;
                                }, op, executer.ConfigInfo, cancellationToken).ContinueOnAnyContext();
                                tcs.SetResult(retryResult);
                            }
                            else
                            {
                                ((OperationResult)result).SetException();
                                tcs.SetResult(result);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        op.Exception = e;
                        op.HandleClientError(e.Message, ResponseStatus.ClientFailure);
                        tcs.SetResult(op.GetResult());
                    }
                }
                else
                {
                    const string msg = "Cannot find callback object for operation: {0}";
                    tcs.TrySetException(new InvalidOperationException(string.Format(msg, s.Opaque)));
                }
            };
            return func;
        }

        public static Func<SocketAsyncState, Task> CompletedFuncForRetry<T>(
            ConcurrentDictionary<uint, IOperation> pending,
            IClusterController controller,
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
                        //check if an error occurred earlier
                        if (s.Exception != null)
                        {
                            actual.Exception = s.Exception;
                            actual.HandleClientError(s.Exception.Message, s.Status);
                            tcs.SetResult(actual.GetResultWithValue());
                            return;
                        }

                        var response = s.Data.ToArray();
                        await op.ReadAsync(response, 0, response.Length)
                            .ContinueOnAnyContext();

                        var result = actual.GetResultWithValue();
                        if (result.IsNmv())
                        {
                            var config = actual.GetConfig();
                            if (config != null)
                            {
                                controller.NotifyConfigPublished(config);
                            }
                        }
                        ((OperationResult)result).SetException();
                        tcs.SetResult(result);
                    }
                    catch (Exception e)
                    {
                        op.Exception = e;
                        op.HandleClientError(e.Message, ResponseStatus.ClientFailure);
                        tcs.SetResult(actual.GetResultWithValue());
                    }
                }
                else
                {
                    const string msg = "Cannot find callback object for operation: {0}";
                    tcs.TrySetException(new InvalidOperationException(string.Format(msg, s.Opaque)));
                }
            };
            return func;
        }

        public static Func<SocketAsyncState, Task> CompletedFuncForRetry(
            ConcurrentDictionary<uint, IOperation> pending,
            IClusterController controller,
            TaskCompletionSource<IOperationResult> tcs)
        {
            Func<SocketAsyncState, Task> func = async s =>
            {
                IOperation op;
                if (pending.TryRemove(s.Opaque, out op))
                {
                    try
                    {
                        //check if an error occurred earlier
                        if (s.Exception != null)
                        {
                            op.Exception = s.Exception;
                            op.HandleClientError(s.Exception.Message, s.Status);
                            tcs.SetResult(op.GetResult());
                            return;
                        }

                        var response = s.Data.ToArray();
                        await op.ReadAsync(response, 0, response.Length)
                            .ContinueOnAnyContext();

                        var result = op.GetResult();
                        if (result.IsNmv())
                        {
                            var config = op.GetConfig();
                            if (config != null)
                            {
                                controller.NotifyConfigPublished(config);
                            }
                        }
                        ((OperationResult)result).SetException();
                        tcs.SetResult(result);
                    }
                    catch (Exception e)
                    {
                        op.Exception = e;
                        op.HandleClientError(e.Message, ResponseStatus.ClientFailure);
                        tcs.SetResult(op.GetResult());
                    }
                }
                else
                {
                    const string msg = "Cannot find callback object for operation: {0}";
                    tcs.TrySetException(new InvalidOperationException(string.Format(msg, s.Opaque)));
                }
            };
            return func;
        }
    }
}
