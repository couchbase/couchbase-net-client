using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Sharding;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Creates a new <see cref="KetamaKeyMapper" />
    /// </summary>
    internal interface IKetamaKeyMapperFactory
    {
        /// <summary>
        /// Creates a new <see cref="KetamaKeyMapper" />
        /// </summary>
        /// <param name="bucketConfig">The <see cref="BucketConfig"/>.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The new <see cref="KetamaKeyMapper"/>.</returns>
        /// <exception cref="InvalidOperationException">IP endpoint lookup failed.</exception>
        Task<KetamaKeyMapper> CreateAsync(BucketConfig bucketConfig, CancellationToken cancellationToken = default);
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
