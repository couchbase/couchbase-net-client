using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// The primary return type for binary Memcached operations which return a value
    /// </summary>
    /// <typeparam name="T">The value returned by the operation.</typeparam>
    public class OperationResult<T> : OperationResult, IOperationResult<T>
    {
        /// <summary>
        /// The value of the key retrieved from Couchbase Server.
        /// </summary>
        public T Content { get; internal set; }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            string content = null;
            try
            {
                content = JsonConvert.SerializeObject(Content, Formatting.None);
            }
            catch
            {
                // ignored
            }
            return new JObject(
                new JProperty("id", Id),
                new JProperty("cas", Cas),
                new JProperty("token", Token?.ToString()),
                new JProperty("content", content)).
                ToString(Formatting.None);
        }
    }
}
#region [ License information ]

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
