using System;

namespace Couchbase.Configuration
{
    /// <summary>
    /// Thrown when the client cannot complete the bootstrapping phase of initialization.
    /// </summary>
    public class CouchbaseBootstrapException : Exception
    {
        public CouchbaseBootstrapException()
        {
        }

        public CouchbaseBootstrapException(string message)
            : base(message)
        {
        }

        public CouchbaseBootstrapException(string message, Exception innerException)
            : base(message, innerException)
        {
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
