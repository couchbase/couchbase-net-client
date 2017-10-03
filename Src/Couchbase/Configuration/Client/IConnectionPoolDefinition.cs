using Couchbase.IO;

namespace Couchbase.Configuration.Client
{
    /// <summary>
    /// Abstracts a configuration definition which can be used to construct a <see cref="ClientConfiguration"/> as
    /// part of a <see cref="ICouchbaseClientDefinition"/>.
    /// </summary>
    public interface IConnectionPoolDefinition
    {
        /// <summary>
        /// The fully qualified type name of the type of the custom <see cref="IConnectionPool"/>.  If null, then
        /// the default connection pool type is used.
        /// </summary>
        /// <value>
        /// The type.
        /// </value>
        string Type { get; }

        /// <summary>
        /// The maximum number of TCP connections that the client will allocate for a given Bucket.
        /// </summary>
        /// <remarks>The default is two TCP connections per bucket.</remarks>
        int MaxSize { get; }

        /// <summary>
        /// The minimum number of TCP connections that the client will allocate for a given bucket.
        /// </summary>
        /// <remarks>The default is one TCP connection per bucket.</remarks>
        /// <remarks>The connection pool will add TCP connections until <see cref="MaxSize"/> is reached.</remarks>
        int MinSize { get; }

        /// <summary>
        /// The amount of time a thread will wait for a <see cref="IConnection"/> once the MaxSize of the pool has been reached and no TCP connections are available.
        /// </summary>
        /// <remarks>The default is 2500ms.</remarks>
        int WaitTimeout { get; }

        /// <summary>
        /// Wait up to the <see cref="ShutdownTimeout"/> to send or recieve data before closing the <see cref="IConnection"/>.
        /// </summary>
        /// <remarks>The default value is 10000ms.</remarks>
        int ShutdownTimeout { get; }

        /// <summary>
        /// Cancels a pending operation if it does not complete in the time given and marks the connection as dead.
        /// </summary>
        /// <remarks>The default value is 15000ms</remarks>
        int SendTimeout { get; }

        /// <summary>
        /// If true, use Secure Socket Layers (SSL) to encrypt traffic between the client and Couchbase server.
        /// </summary>
        /// <remarks>Requires the SSL certificate to be stored in the local Certificate Authority to enable SSL.</remarks>
        /// <remarks>This feature is only supported by Couchbase Cluster 3.0 and greater.</remarks>
        /// <remarks>If the parent <see cref="ICouchbaseClientDefinition"/>'s UseSSL is false, setting this to true will override that configuration and enable the Bucket to use SSL./></remarks>
        bool UseSsl { get; }

        /// <summary>
        /// The size of each buffer to allocate per TCP connection for sending and recieving Memcached operations
        /// </summary>
        /// <remarks>The default is 16K</remarks>
        /// <remarks>The total buffer size is BufferSize * PoolConfiguration.MaxSize</remarks>
        int BufferSize { get; }

        /// <summary>
        /// The amount time allotted for the client to establish a TCP connection with a server before failing
        /// </summary>
        /// <remarks>The default value is 10000ms.</remarks>
        int ConnectTimeout { get; }

        /// <summary>
        /// If true, indicates to enable TCP keep alives.
        /// </summary>
        /// <value>
        /// <c>true</c> to enable TCP keep alives; otherwise, <c>false</c>.
        /// </value>
        /// <remarks>The default is true; TCP Keep Alives are enabled.</remarks>
        bool EnableTcpKeepAlives { get; }

        /// <summary>
        /// Specifies the timeout, in milliseconds, with no activity until the first keep-alive packet is sent.
        /// </summary>
        /// <value>
        /// The TCP keep alive time in milliseconds.
        /// </value>
        /// <remarks>The default is 2hrs.</remarks>
        uint TcpKeepAliveTime { get; }

        /// <summary>
        /// Specifies the interval, in milliseconds, between when successive keep-alive packets are sent if no acknowledgement is received.
        /// </summary>
        /// <value>
        /// The TCP keep alive interval in milliseconds..
        /// </value>
        /// <remarks>The default is 1 second.</remarks>
        uint TcpKeepAliveInterval { get; }

        /// <summary>
        /// The maximum number of times the client will try to close a <see cref="IConnection"/>
        /// if it's in use and <see cref="IConnectionPool"/> has been disposed.
        /// </summary>
        /// <value>
        /// The maximum close attempts.
        /// </value>
        int MaxCloseAttempts { get; }

        /// <summary>
        /// The interval between close attempts on a <see cref="IConnection"/>
        /// if it's in use and <see cref="IConnectionPool"/> has been disposed.
        /// </summary>
        /// <value>
        /// The close attempt interval.
        /// </value>
        uint CloseAttemptInterval { get; }
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
