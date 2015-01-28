using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Couchbase.Helpers;

namespace Couchbase
{
    internal static class GenericViewRowTransformer<T>
    {
        public static T TransformRow(JsonReader reader, ICouchbaseClient client, bool shouldLookupById)
        {
            if (shouldLookupById)
            {
                var key = Json.ParseValue(reader, "id") as string;
                var json = client.Get<string>(key);
                if (string.IsNullOrEmpty(json)) return default(T);

                var jsonWithId = DocHelper.InsertId(json, key);//_id is omitted from the Json return by Get
                return JsonConvert.DeserializeObject<T>(jsonWithId);
            }
            else
            {
                var jObject = Json.ParseValue(reader, "value");
                return JsonConvert.DeserializeObject<T>(jObject);
            }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
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