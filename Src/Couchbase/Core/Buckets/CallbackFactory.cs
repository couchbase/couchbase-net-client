using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Logging;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.Errors;
using Couchbase.Tracing;
using Couchbase.Utils;

namespace Couchbase.Core.Buckets
{
    internal static class CallbackFactory
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CouchbaseRequestExecuter));

        private static OperationHeader CreateHeader(SocketAsyncState state, out ErrorCode errorCode, out long? serverDuration)
        {
            var header = state.CreateHeader(out errorCode);
            serverDuration = header.GetServerDuration(state.Data);

            if (state.DispatchSpan != null)
            {
                state.DispatchSpan.SetPeerLatencyTag(serverDuration);
                state.DispatchSpan.Finish();
            }

            return header;
        }

        private static OperationContext CreateOperationContext(SocketAsyncState state, long? serverDuration, string bucketName = null)
        {
            var context = OperationContext.CreateKvContext(state.Opaque);
            context.ConnectionId = state.ConnectionId;
            context.LocalEndpoint = state.LocalEndpoint;
            context.RemoteEndpoint = state.EndPoint.ToString();
            context.TimeoutMicroseconds = (uint) state.Timeout * 1000; // convert millis to micros

            if (serverDuration.HasValue)
            {
                context.ServerDuration = serverDuration;
            }

            if (!string.IsNullOrWhiteSpace(bucketName))
            {
                context.BucketName = bucketName;
            }

            return context;
        }

        public static Func<SocketAsyncState, Task> CompletedFuncWithRetryForMemcached<T>(IRequestExecuter executer,
            ConcurrentDictionary<uint, IOperation> pending, IClusterController controller,
            TaskCompletionSource<IOperationResult<T>> tcs, CancellationToken cancellationToken)
        {
            Func<SocketAsyncState, Task> func = async s =>
            {
                var header = CreateHeader(s, out var errorCode, out var serverDuration);

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
                        await op.ReadAsync(response, header, errorCode).ContinueOnAnyContext();

                        var result = actual.GetResultWithValue(controller.Configuration.Tracer, executer.ConfigInfo.BucketName);
                        if (result.Success)
                        {
                            tcs.SetResult(result);
                        }
                        else
                        {
                            if (result.IsNmv())
                            {
                                var config = op.GetConfig(controller.ServerConfigTranscoder);
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
                                        if (attempts++ > 10)
                                        {
                                            throw new TimeoutException("Could not acquire a server.");
                                        }
                                        Thread.Sleep((int) Math.Pow(2, attempts));
                                    }
                                    server.SendAsync(o).ContinueOnAnyContext();

                                    return retryTcs.Task;
                                }, actual, executer.ConfigInfo, cancellationToken).ContinueOnAnyContext();
                                tcs.SetResult(retryResult);
                            }
                            else
                            {
                                ((OperationResult) result).SetException();
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
                    finally
                    {
                        s.Dispose();
                    }
                }
                else
                {
                    s.Dispose();
                    const string msg = "Cannot find callback object for operation: {0}";
                    tcs.SetException(new InvalidOperationException(string.Format(msg, s.Opaque)));

                    var context = CreateOperationContext(s, serverDuration, executer.ConfigInfo.BucketName);
                    controller.Configuration.OrphanedResponseLogger.Add(context);
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
                var header = CreateHeader(s, out var errorCode, out var serverDuration);

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
                        await op.ReadAsync(response, header, errorCode).ContinueOnAnyContext();

                        var result = op.GetResult(controller.Configuration.Tracer, executer.ConfigInfo.BucketName);
                        if (result.Success)
                        {
                            tcs.SetResult(result);
                        }
                        else
                        {
                            if (result.IsNmv())
                            {
                                var config = op.GetConfig(controller.ServerConfigTranscoder);
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
                    finally
                    {
                        s.Dispose();
                    }
                }
                else
                {
                    s.Dispose();
                    const string msg = "Cannot find callback object for operation: {0}";
                    tcs.SetException(new InvalidOperationException(string.Format(msg, s.Opaque)));

                    var context = CreateOperationContext(s, serverDuration, executer.ConfigInfo.BucketName);
                    controller.Configuration.OrphanedResponseLogger.Add(context);
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
                var header = CreateHeader(s, out var errorCode, out var serverDuration);

                IOperation op;
                if (pending.TryRemove(s.Opaque, out op))
                {
                    var actual = (IOperation<T>)op;
                    try
                    {
                        if (s.Status == ResponseStatus.TransportFailure)
                        {
                            controller.CheckConfigUpdate(op.BucketName, s.EndPoint);
                        }

                        //check if an error occurred earlier
                        if (s.Exception != null)
                        {
                            actual.Exception = s.Exception;
                            actual.HandleClientError(s.Exception.Message, s.Status);
                            tcs.SetResult(actual.GetResultWithValue());
                            return;
                        }

                        var response = s.Data.ToArray();
                        await op.ReadAsync(response, header, errorCode).ContinueOnAnyContext();

                        var result = actual.GetResultWithValue(controller.Configuration.Tracer, executer.ConfigInfo.BucketName);
                        if (result.Success)
                        {
                            tcs.SetResult(result);
                        }
                        else
                        {
                            if (result.IsNmv())
                            {
                                var config = op.GetConfig(controller.ServerConfigTranscoder);
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
                    finally
                    {
                        s.Dispose();
                    }
                }
                else
                {
                    s.Dispose();
                    const string msg = "Cannot find callback object for operation: {0}";
                    tcs.TrySetException(new InvalidOperationException(string.Format(msg, s.Opaque)));

                    var context = CreateOperationContext(s, serverDuration, executer.ConfigInfo.BucketName);
                    controller.Configuration.OrphanedResponseLogger.Add(context);
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
                var header = CreateHeader(s, out var errorCode, out var serverDuration);

                IOperation op;
                if (pending.TryRemove(s.Opaque, out op))
                {
                    try
                    {
                        if (s.Status == ResponseStatus.TransportFailure)
                        {
                            controller.CheckConfigUpdate(op.BucketName, s.EndPoint);
                        }

                        //check if an error occurred earlier
                        if (s.Exception != null)
                        {
                            op.Exception = s.Exception;
                            op.HandleClientError(s.Exception.Message, s.Status);
                            tcs.SetResult(op.GetResult());
                            return;
                        }

                        var response = s.Data.ToArray();
                        await op.ReadAsync(response, header, errorCode).ContinueOnAnyContext();

                        var result = op.GetResult(controller.Configuration.Tracer, executer.ConfigInfo.BucketName);
                        if (result.Success)
                        {
                            tcs.SetResult(result);
                        }
                        else
                        {
                            if (result.IsNmv())
                            {
                                var config = op.GetConfig(controller.ServerConfigTranscoder);
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
                    finally
                    {
                        s.Dispose();
                    }
                }
                else
                {
                    s.Dispose();
                    const string msg = "Cannot find callback object for operation: {0}";
                    tcs.TrySetException(new InvalidOperationException(string.Format(msg, s.Opaque)));

                    var context = CreateOperationContext(s, serverDuration, executer.ConfigInfo.BucketName);
                    controller.Configuration.OrphanedResponseLogger.Add(context);
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
                var header = CreateHeader(s, out var errorCode, out var serverDuration);

                IOperation op;
                if (pending.TryRemove(s.Opaque, out op))
                {
                    var actual = (IOperation<T>)op;
                    try
                    {
                        if (s.Status == ResponseStatus.TransportFailure)
                        {
                            controller.CheckConfigUpdate(op.BucketName, s.EndPoint);
                        }

                        //check if an error occurred earlier
                        if (s.Exception != null)
                        {
                            actual.Exception = s.Exception;
                            actual.HandleClientError(s.Exception.Message, s.Status);
                            tcs.SetResult(actual.GetResultWithValue());
                            return;
                        }

                        var response = s.Data.ToArray();
                        await op.ReadAsync(response, header, errorCode).ContinueOnAnyContext();

                        var result = actual.GetResultWithValue(controller.Configuration.Tracer, null);
                        if (result.IsNmv())
                        {
                            var config = actual.GetConfig(controller.ServerConfigTranscoder);
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
                    finally
                    {
                        s.Dispose();
                    }
                }
                else
                {
                    s.Dispose();
                    const string msg = "Cannot find callback object for operation: {0}";
                    tcs.TrySetException(new InvalidOperationException(string.Format(msg, s.Opaque)));

                    var context = CreateOperationContext(s, serverDuration);
                    controller.Configuration.OrphanedResponseLogger.Add(context);
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
                var header = CreateHeader(s, out var errorCode, out var serverDuration);

                IOperation op;
                if (pending.TryRemove(s.Opaque, out op))
                {
                    try
                    {
                        if (s.Status == ResponseStatus.TransportFailure)
                        {
                            controller.CheckConfigUpdate(op.BucketName, s.EndPoint);
                        }

                        //check if an error occurred earlier
                        if (s.Exception != null)
                        {
                            op.Exception = s.Exception;
                            op.HandleClientError(s.Exception.Message, s.Status);
                            tcs.SetResult(op.GetResult());
                            return;
                        }

                        var response = s.Data.ToArray();
                        await op.ReadAsync(response, header, errorCode).ContinueOnAnyContext();

                        var result = op.GetResult(controller.Configuration.Tracer, null);
                        if (result.IsNmv())
                        {
                            var config = op.GetConfig(controller.ServerConfigTranscoder);
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
                    finally
                    {
                        s.Dispose();
                    }
                }
                else
                {
                    s.Dispose();
                    const string msg = "Cannot find callback object for operation: {0}";
                    tcs.TrySetException(new InvalidOperationException(string.Format(msg, s.Opaque)));

                    var context = CreateOperationContext(s, serverDuration);
                    controller.Configuration.OrphanedResponseLogger.Add(context);
                }
            };
            return func;
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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

#endregion
