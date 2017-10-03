using Newtonsoft.Json;

namespace Couchbase.Management
{
    /// <summary>
    /// Represents a Couchbase role that allows a cluster operation such as KV Read / Write, Select N1ql, etc.
    /// Roles can also be bucket specific.
    /// </summary>
    public class Role
    {
        [JsonProperty("role")]
        public string Name { get; set; }

        [JsonProperty("bucket_name")]
        public string BucketName { get; set; }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
