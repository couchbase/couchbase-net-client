using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.Logging;
using System.Security.Authentication;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations.Authentication;
using Couchbase.Utils;

namespace Couchbase.IO
{
    public abstract class ConnectionPoolBase<T> : IConnectionPool<T> where T : class, IConnection
    {
        private static readonly ILog Log = LogManager.GetLogger<ConnectionPoolBase<T>>();
        protected readonly Guid Identity = Guid.NewGuid();
        protected IByteConverter Converter;
        protected BufferAllocator BufferAllocator;
        protected internal Func<IConnectionPool<T>, IByteConverter, BufferAllocator, T> Factory;

        //for log redaction
        private Func<object, string> User = RedactableArgument.UserAction;

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedConnectionPool{T}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="endPoint">The remote endpoint or server node to connect to.</param>
        protected ConnectionPoolBase(PoolConfiguration configuration, IPEndPoint endPoint)
            : this(configuration, endPoint, DefaultConnectionFactory.GetGeneric<T>(), new DefaultConverter())
        {
        }

        /// <summary>
        /// CTOR for testing/dependency injection.
        /// </summary>
        /// <param name="configuration">The <see cref="PoolConfiguration"/> to use.</param>
        /// <param name="endPoint">The <see cref="IPEndPoint"/> of the Couchbase Server.</param>
        /// <param name="factory">A functory for creating <see cref="IConnection"/> objects./></param>
        /// <param name="converter">The <see cref="IByteConverter"/>that this instance is using.</param>
        internal ConnectionPoolBase(PoolConfiguration configuration, IPEndPoint endPoint, Func<IConnectionPool<T>, IByteConverter, BufferAllocator, T> factory, IByteConverter converter)
        {
            Configuration = configuration;
            Factory = factory;
            Converter = converter;
            BufferAllocator = Configuration.BufferAllocator(Configuration);
            EndPoint = endPoint;
        }

        public abstract void Release(T connection);
        public abstract void Dispose();
        public abstract IConnection Acquire();
        public abstract void Initialize();
        public virtual IEnumerable<IConnection> Connections { get; set; }

        /// <summary>
        /// Gets or sets the sasl mechanism used to authenticate against the server.
        /// </summary>
        /// <value>
        /// The sasl mechanism.
        /// </value>
        public ISaslMechanism SaslMechanism { get; set; }

        /// <summary>
        /// Gets a value indicating whether the pool failed to initialize properly.
        /// If for example, TCP connection to the server couldn't be made, then this
        /// would return false until the connection could be made (after the node went
        /// back online).
        /// </summary>
        /// <value>
        ///   <c>true</c> if initialization failed; otherwise, <c>false</c>.
        /// </value>
        public bool InitializationFailed { get; protected set; }

        /// <summary>
        /// The configuration passed into the pool when it is created. It has fields
        /// for MaxSize, MinSize, etc.
        /// </summary>
        public PoolConfiguration Configuration { get; protected set; }

        /// <summary>
        /// The <see cref="IPEndPoint"/> of the server that the <see cref="IConnection"/>s are connected to.
        /// </summary>
        public IPEndPoint EndPoint { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="IServer" /> instance which "owns" this pool.
        /// </summary>
        /// <value>
        /// The owner.
        /// </value>
        public IServer Owner { get; set; }

        public virtual int Count()
        {
            if (Connections == null) return 0;
            return Connections.Count();
        }

        void IConnectionPool.Release(IConnection connection)
        {
            Release((T)connection);
        }

        protected void Authenticate(IConnection connection)
        {
            if (Configuration.ClientConfiguration?.EnableCertificateAuthentication ?? false)
            {
                Log.Trace("Ignoring authentication using x509 cert.");
                //ignore auth if cert auth is being used
                return;
            }
            Log.Trace("1. Checking authentication [{0}|{1}]: {2} - {3}", connection.IsAuthenticated,
                connection.IsDead, EndPoint, connection.Identity);

            if (connection.IsAuthenticated || connection.IsDead) return;

            Log.Trace("2. Checking authentication [{0}|{1}]: {2} - {3}", connection.IsAuthenticated,
                connection.IsDead, EndPoint, connection.Identity);

            if (SaslMechanism != null)
            {
                Log.Trace("3. Checking authentication [{0}]: {1} - {2}", connection.IsAuthenticated, EndPoint,
                    connection.Identity);
                var result = SaslMechanism.Authenticate(connection);
                if (result)
                {
                    Log.Info(
                        "4. Authenticated {0} using {1} - {2} - {3} [{4}].", User(SaslMechanism.Username),
                        SaslMechanism.GetType(),
                        Identity, connection.Identity, EndPoint);
                    connection.IsAuthenticated = true;
                }
                else
                {
                    Log.Info(
                        "4. Could not authenticate {0} using {1} - {2} [{3}].", User(SaslMechanism.Username),
                        SaslMechanism.GetType(), Identity, EndPoint);

                    connection.IsDead = true;
                    connection.Dispose();

                    var message = SupportsEnhancedAuthentication
                        ? ExceptionUtil.FailedUserAuthenticationMsg.WithParams(User(SaslMechanism.Username))
                        : ExceptionUtil.FailedBucketAuthenticationMsg.WithParams(Configuration.BucketName);

                    throw new AuthenticationException(message);
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the server supports enhanced authentication for RBAC.
        /// </summary>
        /// <value>
        /// <c>true</c> if the server supports enhanced authentication; otherwise, <c>false</c>.
        /// </value>
        public bool SupportsEnhancedAuthentication
        {
            get;
            set;
        }

        /// <summary>
        /// Enables enhanced authentication for RBAC on a connection.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <exception cref="AuthenticationException"></exception>
        protected void EnableEnhancedAuthentication(IConnection connection)
        {
            if (SupportsEnhancedAuthentication && !connection.CheckedForEnhancedAuthentication) // only execute this if RBAC is enabled on the cluster
            {
                Log.Debug("Calling SelectBucket on node {0}.", connection.EndPoint);
                if (string.IsNullOrWhiteSpace(Configuration.BucketName))
                {
                    const string bucketNameIsEmptyMessage = "BucketName cannot be empty when sending SelectBucket operation";
                    Log.Error(bucketNameIsEmptyMessage);
                    throw new AuthenticationException(bucketNameIsEmptyMessage);
                }

                var selectBucketOp = new SelectBucket(Configuration.BucketName, new DefaultTranscoder(), 0);
                var response = connection.Send(selectBucketOp.Write());
                selectBucketOp.Read(response);

                var selectBucketResult = selectBucketOp.GetResult();
                connection.CheckedForEnhancedAuthentication = true;

                if (!selectBucketResult.Success)
                {
                    Log.Error($"Failed to perform SelectBucket operation for '{Configuration.BucketName}'. Reason {selectBucketResult.Status}.");
                    throw new AuthenticationException(ExceptionUtil.FailedUserAuthenticationMsg.WithParams(Configuration.BucketName));
                }
            }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
