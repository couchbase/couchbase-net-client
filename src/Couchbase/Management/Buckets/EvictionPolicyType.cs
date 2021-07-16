using System.ComponentModel;

namespace Couchbase.Management.Buckets
{
    public enum EvictionPolicyType
    {
        /// <summary>
        ///     During ejection, everything (including key, metadata, and value) will be ejected.
        ///     Full Ejection reduces the memory overhead requirement, at the cost of performance.
        ///     This value is only valid for buckets of type <see cref="BucketType.Couchbase" />
        /// </summary>
        [Description("fullEviction")] FullEviction = 0,

        /// <summary>
        ///     During ejection, only the value will be ejected (key and metadata will remain in memory).
        ///     Value Ejection needs more system memory, but provides better performance than Full Ejection.
        ///     This value is only valid for buckets of type <see cref="BucketType.Couchbase" />
        /// </summary>
        [Description("valueOnly")] ValueOnly = 1,


        /// <summary>
        ///     When the memory quota is reached, Couchbase Server ejects data that has not been used recently.
        ///     This value is only valid for buckets of type <see cref="BucketType.Ephemeral" />
        /// </summary>
        [Description("nruEviction")] NotRecentlyUsed = 2,

        /// <summary>
        ///     Couchbase Server keeps all data until explicitly deleted, but will reject
        ///     any new data if you reach the quota(dedicated memory) you set for your bucket.
        ///     This value is only valid for buckets of type <see cref="BucketType.Ephemeral" />
        /// </summary>
        [Description("noEviction")] NoEviction = 3
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
