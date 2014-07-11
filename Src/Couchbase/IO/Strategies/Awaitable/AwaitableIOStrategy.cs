using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Strategies.Awaitable
{
    /// <summary>
    /// An IO strategy that leverages the Task Asynchrony Pattern (TAP) so that IO operations can be awaited on.
    /// </summary>
    internal sealed class AwaitableIOStrategy : IOStrategy
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IConnectionPool _connectionPool;
        private readonly SocketAwaitablePool _socketAwaitablePool;
        private readonly IByteConverter _converter;
        private volatile bool _disposed;

        public AwaitableIOStrategy(IConnectionPool connectionPool) 
            : this(connectionPool, new SocketAwaitablePool(connectionPool, SocketAwaitableFactory.GetSocketAwaitable()), new AutoByteConverter())
        {
        }
       
        public AwaitableIOStrategy(IConnectionPool connectionPool, SocketAwaitablePool socketAwaitablePool, IByteConverter converter)
        {
            _connectionPool = connectionPool;
            _socketAwaitablePool = socketAwaitablePool;
            _converter = converter;
        }

        /// <summary>
        /// The <see cref="IPEndPoint"/> of the Couchbase Server we are connected to.
        /// </summary>
        public IPEndPoint EndPoint
        {
            get { return _connectionPool.EndPoint; }
        }

        /// <summary>
        /// The pool of <see cref="IConnection"/> objects to use for TCP operations.
        /// </summary>
        public IConnectionPool ConnectionPool
        {
            get { return _connectionPool; }
        }

        /// <summary>
        /// The <see cref="SaslMechanism"/> supported and used for authenticating <see cref="IConnection"/>s.
        /// </summary>
        public ISaslMechanism SaslMechanism
        {
            set { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Executes a synchronous operation
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="operation"></param>
        /// <param name="connection"></param>
        /// <returns></returns>
        public IOperationResult<T> Execute<T>(IOperation<T> operation, IConnection connection)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Executes a synchronous operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="operation"></param>
        /// <returns></returns>
        public IOperationResult<T> Execute<T>(IOperation<T> operation)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Executes an asynchronous operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="operation"></param>
        /// <returns></returns>
        public async Task<IOperationResult<T>> ExecuteAsync<T>(IOperation<T> operation)
        {
            var socketAwaitable = _socketAwaitablePool.Acquire();
            var socketAsync = socketAwaitable.EventArgs;

            operation.Reset();
            var buffer = operation.Write();
            Log.Debug(m => m("writing buffer...{0} bytes", buffer.Length));

             socketAsync.SetBuffer(buffer, 0, buffer.Length);

            Log.Debug(m => m("sending buffer...{0} bytes", buffer.Length));
 
            await socketAwaitable.SendAsync();
            await Receive(operation, socketAwaitable);

            Log.Debug(m => m("sent buffer...{0} bytes", buffer.Length));
            _socketAwaitablePool.Release(socketAwaitable);
            return operation.GetResult();
        }

        /// <summary>
        /// Executes an asynchronous operation.
        /// </summary>
        public async Task<IOperationResult<T>> ExecuteAsync<T>(IOperation<T> operation, IConnection connection)
        {
            var eventArgs = new SocketAsyncEventArgs
            {
                AcceptSocket = connection.Socket,
                UserToken = new OperationAsyncState
                {
                    Connection = connection
                }
            };
            var socketAwaitable = new SocketAwaitable(eventArgs);
            
            var buffer = operation.GetBuffer();
            socketAwaitable.EventArgs.SetBuffer(buffer, 0, buffer.Length);

            await socketAwaitable.SendAsync();
            await Receive(operation, socketAwaitable);
            return operation.GetResult();
        }

        /// <summary>
        /// Recieves data from the remote server.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="operation"></param>
        /// <param name="socketAwaitable"></param>
        /// <returns></returns>
        private async Task Receive<T>(IOperation<T> operation, SocketAwaitable socketAwaitable)
        {
            var eventArgs = socketAwaitable.EventArgs;
            var state = (OperationAsyncState)eventArgs.UserToken;
            socketAwaitable.EventArgs.SetBuffer(state.Buffer, 0, state.Buffer.Length);
            
            do
            {
                await socketAwaitable.ReceiveAsync();
                state.BytesReceived += eventArgs.BytesTransferred;
                state.Data.Write(eventArgs.Buffer, eventArgs.Offset, eventArgs.Count);
                Log.Debug(m => m("receive...{0} bytes", state.BytesReceived));

                if (operation.Header.BodyLength == 0)
                {
                    CreateHeader(operation, state);
                }
            } 
            while (state.BytesReceived < operation.Header.TotalLength);

            CreateBody(operation, state);
            state.Reset();
        }

        /// <summary>
        /// Creates an <see cref="OperationHeader"/> for the current operation in progress.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="operation"></param>
        /// <param name="state"></param>
        void CreateHeader<T>(IOperation<T> operation, OperationAsyncState state)
        {
            var buffer = state.Data.GetBuffer();
            if (buffer.Length > 0)
            {
                operation.Header = new OperationHeader
                {
                    Magic = _converter.ToByte(buffer, HeaderIndexFor.Magic),
                    OperationCode = _converter.ToByte(buffer, HeaderIndexFor.Opcode).ToOpCode(),
                    KeyLength = _converter.ToInt16(buffer, HeaderIndexFor.KeyLength),
                    ExtrasLength = _converter.ToByte(buffer, HeaderIndexFor.ExtrasLength),
                    Status = (ResponseStatus)_converter.ToInt16(buffer, HeaderIndexFor.Status),
                    BodyLength = _converter.ToInt32(buffer, HeaderIndexFor.Body),
                    Opaque = _converter.ToUInt32(buffer, HeaderIndexFor.Opaque),
                    Cas = _converter.ToUInt64(buffer, HeaderIndexFor.Cas)
                };
            }
        }

        /// <summary>
        /// Creates the <see cref="OperationBody"/> of the current operation in progress.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="operation"></param>
        /// <param name="state"></param>
        static void CreateBody<T>(IOperation<T> operation, OperationAsyncState state)
        {
            var buffer = state.Data.GetBuffer();
            operation.Body = new OperationBody
            {
                Extras = new ArraySegment<byte>(buffer, OperationBase<T>.HeaderLength, operation.Header.ExtrasLength).Array,
                Data = new ArraySegment<byte>(buffer, 28, operation.Header.BodyLength).Array,
            };
        }

        /// <summary>
        /// Disposes this object and it's internal <see cref="IConnectionPool"/> object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        void Dispose(bool disposing)
        {
            if (_disposed)
            {
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
                _connectionPool.Dispose();
                _disposed = true;
            }
        }

        ~AwaitableIOStrategy()
        {
            Dispose(false);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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