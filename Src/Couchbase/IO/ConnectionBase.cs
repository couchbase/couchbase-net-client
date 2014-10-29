using System;
using System.Deployment.Internal;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using Common.Logging;
using Couchbase.Configuration.Client;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies;
using Couchbase.IO.Strategies.Awaitable;
using Couchbase.IO.Utils;

namespace Couchbase.IO
{
    internal abstract class ConnectionBase : IConnection
    {
        protected readonly static ILog Log = LogManager.GetCurrentClassLogger();
        protected readonly Guid _identity = Guid.NewGuid();
        private readonly Socket _socket;
        private readonly OperationAsyncState _state;
        private readonly IByteConverter _converter;
        protected readonly BufferManager BufferManager;
        protected readonly AutoResetEvent SendEvent = new AutoResetEvent(false);
        protected IConnectionPool ConnectionPool;
        protected PoolConfiguration Configuration;
        protected volatile bool Disposed;
        private volatile bool _isDead;

        protected ConnectionBase(Socket socket, IByteConverter converter)
            : this(socket, new OperationAsyncState(), converter, BufferManager.CreateBufferManager(1024*1000, 1024))
        {
        }

        protected ConnectionBase(Socket socket, OperationAsyncState asyncState, IByteConverter converter, BufferManager bufferManager)
        {
            _socket = socket;
            _state = asyncState;
            _converter = converter;
            BufferManager = bufferManager;
            EndPoint = socket.RemoteEndPoint;
        }

        protected ConnectionBase(Socket socket, OperationAsyncState asyncState, IByteConverter converter, BufferManager bufferManager, IPEndPoint endPoint)
        {
            _socket = socket;
            _state = asyncState;
            _converter = converter;
            BufferManager = bufferManager;
            EndPoint = endPoint;
        }

        public OperationAsyncState State
        {
            get { return _state; }
        }

        /// <summary>
        /// The Socket used for IO.
        /// </summary>
        public Socket Socket
        {
            get { return _socket; }
        }

        /// <summary>
        /// Unique identifier for this connection.
        /// </summary>
        public Guid Identity
        {
            get { return _identity; }
        }

        /// <summary>
        /// True if the connection has been SASL authenticated.
        /// </summary>
        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// True if connection is using SSL
        /// </summary>
        public bool IsSecure { get; protected set; }

        public abstract IOperationResult<T> Send<T>(IOperation<T> operation); 

        [Obsolete]
        protected void CreateHeader(OperationAsyncState state)
        {
            var buffer = state.Data.GetBuffer();
            if (buffer.Length > 0)
            {
                state.Header = new OperationHeader
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

        [Obsolete]
        protected static void CreateBody(OperationAsyncState state)
        {
            var buffer = state.Data.GetBuffer();
            if (buffer.Length > 0)
            {
                state.Body = new OperationBody
                {
                    Extras =state.Header.ExtrasLength > 0 ?
                      new ArraySegment<byte>(buffer, OperationBase<object>.HeaderLength, state.Header.ExtrasLength).Array :
                      new ArraySegment<byte>().Array,
                    Data = new ArraySegment<byte>(buffer, state.Offset, state.Header.BodyLength-state.Header.ExtrasLength).Array
                };
            }
        }

        public abstract void Dispose();

        protected void HandleException(Exception e, IOperation operation)
        {
            try
            {
                var message = string.Format("Opcode={0} | Key={1} | Host={2}",
                    operation.OperationCode,
                    operation.Key,
                    ConnectionPool.EndPoint);

                Log.Warn(message, e);
                operation.HandleClientError("Failed. Check Exception property.", ResponseStatus.ClientFailure);
                operation.Exception = e;
            }
            finally
            {
                SendEvent.Set();
            }
        }

        public EndPoint EndPoint { get; private set; }


        public bool IsDead
        {
            get { return _isDead; }
            set { _isDead = value; }
        }
    }
}

#region [ License information ]

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