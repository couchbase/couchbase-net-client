namespace Couchbase.Core.Buckets
{
    /// <summary>
    /// Represents the Bucket types supported by Couchbase Server
    /// </summary>
    public enum BucketTypeEnum
    {
        /// <summary>
        /// A persistent Bucket supporting replication and rebalancing.
        /// </summary>
        Couchbase = 0x00,
        
        /// <summary>
        /// A Bucket supporting in-memory Key/Value operations.
        /// </summary>
        Memcached = 0x01
    }
}

#region [ License information          ]

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