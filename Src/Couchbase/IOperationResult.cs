using Couchbase.IO;
using Couchbase.IO.Operations;

namespace Couchbase
{
    /// <summary>
    /// The primary return type for binary Memcached operations
    /// </summary>
    public interface IOperationResult : IResult
    {
        /// <summary>
        /// The 'Check and Set' or 'CAS' value for enforcing optimistic concurrency.
        /// </summary>
        ulong Cas { get; }

        /// <summary>
        /// The server's response status for the operation.
        /// </summary>
        ResponseStatus Status { get; }

        /// <summary>
        /// The level of durability that the operation achieved
        /// </summary>
        Durability Durability { get; set; }
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
