using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.IO
{
    /// <summary>
    /// Thrown when an available <see cref="IConnection"/> cannot be obtained from the <see cref="IConnectionPool"/> after n number of tries.
    /// </summary>
    public sealed class ConnectionUnavailableException : Exception
    {
        public ConnectionUnavailableException()
        {
        }

        public ConnectionUnavailableException(string message, params object[] args)
            : base(string.Format(message, args))
        {
        }

        public ConnectionUnavailableException(string message)
            : base(message)
        {
        }

        public ConnectionUnavailableException(string message, Exception innerException)
            : base(message, innerException)
        {
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
