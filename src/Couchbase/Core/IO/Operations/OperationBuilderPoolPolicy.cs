using Microsoft.Extensions.ObjectPool;

namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// Policy which handles creating and returning <see cref="OperationBuilder"/> instances to the
    /// <see cref="ObjectPool{T}"/>.
    /// </summary>
    internal class OperationBuilderPoolPolicy : PooledObjectPolicy<OperationBuilder>
    {
        /// <summary>
        /// Returned operation builders with a capacity larger than this limit are disposed rather than retained.
        /// </summary>
        public int MaximumOperationBuilderCapacity { get; set; } = 1024 * 1024; // 1MB

        /// <inheritdoc />
        public override OperationBuilder Create() => new();

        /// <inheritdoc />
        public override bool Return(OperationBuilder obj)
        {
            // Note that DefaultObjectPoolProvider.Create creates a pool which already handles
            // calling Dispose for us if this method returns false OR if the pool size is exceeded.
            // Therefore, we don't need to call Dispose here.

            if (obj.Capacity > MaximumOperationBuilderCapacity)
            {
                return false;
            }

            obj.Reset();
            return true;
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
