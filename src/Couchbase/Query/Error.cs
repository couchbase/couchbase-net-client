using Couchbase.Core.Compatibility;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Couchbase.Query
{
    public class Error
    {
        [JsonProperty("msg")]
        public string Message { get; set; }

        public int Code { get; set; }

        public string Name { get; set; }

        public Severity Severity { get; set; }

        public bool Temp { get; set; }

        [InterfaceStability(Level.Volatile)]
        [JsonExtensionData]
        public IDictionary<string, JToken> AdditionalData { get; set; }
    }

    internal class ErrorData
    {
        public string msg { get; set; }
        public int code { get; set; }
        public string name { get; set; }
        public Severity sev { get; set; }
        public bool temp { get; set; }

        internal Error ToError()
        {
            return new Error
            {
                Message = msg,
                Code = code,
                Name = name,
                Severity = sev,
                Temp = temp,
            };
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
