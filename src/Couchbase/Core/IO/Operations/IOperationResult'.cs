using System;

#nullable enable

namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// The primary return type for binary Memcached operations which return a value
    /// </summary>
    /// <typeparam name="T">The value returned by the operation.</typeparam>
    [Obsolete("This interface is not required and will be removed in a future release.")] // Delete
    public interface IOperationResult<out T> : IOperationResult
    {
        /// <summary>
        /// The value returned by the operation.
        /// </summary>
        T? Content { get;}
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
