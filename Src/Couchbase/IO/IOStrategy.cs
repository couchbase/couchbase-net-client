using System;
using System.Net;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Server.Providers;
using Couchbase.Configuration.Server.Providers.Streaming;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.IO.Operations;

namespace Couchbase.IO
{
    /// <summary>
    /// Primary interface for the IO engine.
    /// </summary>
    internal interface IOStrategy : IDisposable
    {
        /// <summary>
        /// Executes an operation for a given key.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="operation"></param>
        /// <param name="connection"></param>
        /// <returns></returns>
        IOperationResult<T> Execute<T>(IOperation<T> operation, IConnection connection);

        IOperationResult<T> Execute<T>(IOperation<T> operation);

        IPEndPoint EndPoint { get; }

        IConnectionPool ConnectionPool { get; }

        ISaslMechanism SaslMechanism { set; }
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