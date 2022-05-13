using System;
using Couchbase.Core.Configuration.Server;
using Couchbase.Management.Buckets;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Creates a <seealso cref="BucketBase"/> based on <seealso cref="BucketType"/>.
    /// </summary>
    internal interface IBucketFactory
    {
        /// <summary>
        /// Creates a <seealso cref="BucketBase"/> based on <seealso cref="BucketType"/>.
        /// </summary>
        /// <param name="name">Name of the bucket.</param>
        /// <param name="bucketType">Type of the bucket.</param>
        /// <param name="config">The initial bootstrap cluster map config.</param>
        /// <returns>Correct bucket implementation.</returns>
        BucketBase Create(string name, BucketType bucketType, BucketConfig config);
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
