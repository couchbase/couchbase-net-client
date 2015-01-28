﻿using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    public sealed class Ram
    {
        [JsonProperty("total")]
        public long Total { get; set; }

        [JsonProperty("quotaTotal")]
        public long QuotaTotal { get; set; }

        [JsonProperty("quotaUsed")]
        public long QuotaUsed { get; set; }

        [JsonProperty("used")]
        public long Used { get; set; }

        [JsonProperty("usedByData")]
        public long UsedByData { get; set; }
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