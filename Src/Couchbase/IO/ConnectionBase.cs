﻿using System;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Configuration.Client;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;

namespace Couchbase.IO
{
    public abstract class ConnectionBase : IConnection
    {
        protected readonly static ILog Log = LogManager.GetLogger<ConnectionBase>();
        protected readonly Guid _identity = Guid.NewGuid();
        private readonly Socket _socket;
        private readonly OperationAsyncState _state;
        protected readonly IByteConverter Converter;
        protected readonly BufferManager BufferManager;
        protected readonly AutoResetEvent SendEvent = new AutoResetEvent(false);
        protected IConnectionPool ConnectionPool;
        protected PoolConfiguration Configuration;
        protected volatile bool Disposed;
        private volatile bool _isDead;
        private volatile bool _inUse = false;
        private System.Timers.Timer _timer;
        private int _closeAttempts;

        protected ConnectionBase(Socket socket, IByteConverter converter)
            : this(socket, new OperationAsyncState(), converter, BufferManager.CreateBufferManager(1024 * 1000, 1024))
        {
        }

        internal ConnectionBase(Socket socket, OperationAsyncState asyncState, IByteConverter converter, BufferManager bufferManager)
        {
            _socket = socket;
            _state = asyncState;
            Converter = converter;
            BufferManager = bufferManager;
            EndPoint = socket.RemoteEndPoint;
        }

        internal ConnectionBase(Socket socket, OperationAsyncState asyncState, IByteConverter converter, BufferManager bufferManager, IPEndPoint endPoint)
        {
            _socket = socket;
            _state = asyncState;
            Converter = converter;
            BufferManager = bufferManager;
            EndPoint = endPoint;
        }

        internal OperationAsyncState State
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
        /// Gets or sets the write buffer.
        /// </summary>
        /// <value>
        /// The write buffer for building the request packet.
        /// </value>
        public byte[] WriteBuffer { get; set; }

        /// <summary>
        /// True if connection is using SSL
        /// </summary>
        public bool IsSecure { get; protected set; }

        /// <summary>
        /// Gets the remote hosts <see cref="EndPoint"/> that this <see cref="Connection"/> is connected to.
        /// </summary>
        /// <value>
        /// The end point.
        /// </value>
        public EndPoint EndPoint { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is dead.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is dead; otherwise, <c>false</c>.
        /// </value>
        public bool IsDead
        {
            get { return _isDead; }
            set { _isDead = value; }
        }

        /// <summary>
        /// Gets or sets the maximum times that the client will check the <see cref="InUse"/>
        /// property before closing the connection.
        /// </summary>
        /// <value>
        /// The maximum close attempts.
        /// </value>
        public int MaxCloseAttempts { get; set; }

        /// <summary>
        ///  Checks whether this <see cref="Connection"/> is currently being used to execute a request.
        /// </summary>
        /// <value>
        ///   <c>true</c> if if this <see cref="Connection"/> is in use; otherwise, <c>false</c>.
        /// </value>
        public bool InUse { get { return _inUse; } }

        /// <summary>
        /// Gets the number of close attempts that this <see cref="Connection"/> has attemped.
        /// </summary>
        /// <value>
        /// The close attempts.
        /// </value>
        public int CloseAttempts { get { return _closeAttempts; } }

        /// <summary>
        /// Gets a value indicating whether this instance is shutting down.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance has shutdown; otherwise, <c>false</c>.
        /// </value>
        public bool HasShutdown { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance is disposed; otherwise, <c>false</c>.
        /// </value>
        public bool IsDisposed { get { return Disposed; } }

        protected virtual void HandleException(Exception e, IOperation operation)
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

        public virtual void Send<T>(IOperation<T> operation)
        {
            throw new NotImplementedException();
        }

        public virtual byte[] Send(byte[] request)
        {
            throw new NotImplementedException();
        }

        public virtual void SendAsync(byte[] request, Func<SocketAsyncState, Task> callback)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Marks this <see cref="Connection"/> as used; meaning it cannot be disposed unless <see cref="InUse"/>
        /// is <c>false</c> or the <see cref="MaxCloseAttempts"/> has been reached.
        /// </summary>
        /// <param name="isUsed">if set to <c>true</c> [is used].</param>
        public void MarkUsed(bool isUsed)
        {
            _inUse = isUsed;
        }

        /// <summary>
        /// Disposes this <see cref="Connection"/> if <see cref="InUse"/> is <c>false</c>; otherwise
        /// it will wait for the interval and attempt again up until the <see cref="MaxCloseAttempts"/>
        /// threshold is met or <see cref="InUse"/> is <c>false</c>.
        /// </summary>
        /// <param name="interval">The interval to wait between close attempts.</param>
        public void CountdownToClose(uint interval)
        {
            HasShutdown = true;
            _timer = new System.Timers.Timer
            {
                Interval = interval,
                AutoReset = false,
                Enabled = true
            };

            //callback for timer
            _timer.Elapsed += (o, args) =>
            {
                _closeAttempts = Interlocked.Increment(ref _closeAttempts);
                if (InUse && _closeAttempts < MaxCloseAttempts && !IsDead)
                {
                    Log.DebugFormat("Restarting timer for connection for {0} after {1}", _identity, args.SignalTime.Millisecond);
                    _timer.Start();
                }
                else
                {
                    //mark dead
                    IsDead = true;

                    //this will call the derived classes Dispose method,
                    //which call the base.Dispose (on OperationBase) cleaning up the timer.
                    Dispose();
                    Log.DebugFormat("Disposing {0} after {1}ms", _identity, args.SignalTime.Millisecond);
                }
            };
        }

        /// <summary>
        /// Disposes the <see cref="Timer"/> used for checking whether or not the connection
        /// is in use and can be Disposed; <see cref="InUse"/> will be set to <c>false</c>.
        /// </summary>
        public virtual void Dispose()
        {
            Log.DebugFormat("Disposing the timer for {0}", _identity);
            if (_timer == null) return;
            _inUse = false;
            _timer.Dispose();
        }

        /// <summary>
        /// Authenticates this instance.
        /// </summary>
        public virtual void Authenticate()
        {
            //noop
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