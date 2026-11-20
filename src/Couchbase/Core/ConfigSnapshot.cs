using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Sharding;

#nullable enable

namespace Couchbase.Core
{
    /// <summary>
    /// A bucket config and the key mapper that maps keys for it. An operation reads one snapshot,
    /// so its vBucket and its server indexes come from the same server list.
    /// </summary>
    /// <remarks>
    /// The key mapper can come from an older config with the same vBucket server map.
    /// </remarks>
    internal sealed class ConfigSnapshot
    {
        public static readonly ConfigSnapshot Empty = new(null, null);

        public ConfigSnapshot(BucketConfig? config, IKeyMapper? keyMapper)
        {
            Config = config;
            KeyMapper = keyMapper;
        }

        public BucketConfig? Config { get; }

        public IKeyMapper? KeyMapper { get; }
    }
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
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
