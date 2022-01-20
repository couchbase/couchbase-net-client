using System;
using System.Threading;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Sharding;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Default implementation of <see cref="IVBucketKeyMapperFactory"/>.
    /// </summary>
    internal class VBucketKeyMapperFactory : IVBucketKeyMapperFactory
    {
        private readonly IVBucketFactory _vBucketFactory;

        public VBucketKeyMapperFactory(IVBucketFactory vBucketFactory)
        {
            _vBucketFactory = vBucketFactory ?? throw new ArgumentNullException(nameof(vBucketFactory));
        }

        /// <inheritdoc />
        public IKeyMapper Create(BucketConfig bucketConfig,
            CancellationToken cancellationToken = default) =>
            new VBucketKeyMapper(
                bucketConfig,
                new VBucketServerMap(bucketConfig.VBucketServerMap),
                _vBucketFactory);
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
