using Couchbase.IO;

namespace Couchbase.Authentication.SASL
{
    /// <summary>
    /// Provides and interface for implementating a SASL authentication mechanism (CRAM MD5 or PLAIN).
    /// </summary>
    public interface ISaslMechanism
    {
        /// <summary>
        /// The username or Bucket name.
        /// </summary>
        string Username { get; }

        /// <summary>
        /// The password to authenticate against.
        /// </summary>
        string Password { get; }

        /// <summary>
        /// The type of SASL mechanism to use: PLAIN or CRAM MD5.
        /// </summary>
        string MechanismType { get; }

        /// <summary>
        /// Authenticates a username and password using a specific <see cref="IConnection"/> instance.
        /// </summary>
        /// <param name="connection">An implementation of <see cref="IConnection"/> which represents a TCP connection to a Couchbase Server.</param>
        /// <param name="username">The username or bucket name to authentic against.</param>
        /// <param name="password">The password to authenticate against.</param>
        /// <returns>True if succesful.</returns>
        bool Authenticate(IConnection connection, string username, string password);

        /// <summary>
        /// Authenticates a username and password.
        /// </summary>
        /// <param name="connection">An implementation of <see cref="IConnection"/> which represents a TCP connection to a Couchbase Server.</param>
        /// <returns>True if succesful.</returns>
        bool Authenticate(IConnection connection);

        /// <summary>
        /// The I/O service to use <see cref="IOService"/>
        /// </summary>
        IIOService IOService { set; }
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