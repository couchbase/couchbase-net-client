using System;
using System.Collections.Generic;
using System.Net;
using Couchbase.Core.Sharding;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Default implementation of <see cref="IVBucketFactory"/>.
    /// </summary>
    internal class VBucketFactory : IVBucketFactory
    {
        private readonly ILogger<VBucket> _logger;

        public VBucketFactory(ILogger<VBucket> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public IVBucket Create(ICollection<HostEndpointWithPort> endPoints, short index, short primary,
            short[] replicas, ulong rev, VBucketServerMap vBucketServerMap, string bucketName) =>
            new VBucket(endPoints, index, primary, replicas, rev, vBucketServerMap, bucketName, _logger);
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
