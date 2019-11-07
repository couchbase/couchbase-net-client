using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Couchbase.Core.IO.Operations.Errors
{
    /// <summary>
    /// Describes an error received from the server including name, description and retry stratergy.
    /// </summary>
    public class ErrorCode
    {
        public ErrorCode()
        {
            Retry = new RetrySpec();
        }

        /// <summary>
        /// Gets or sets the name of the error.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the description of the error.
        /// </summary>
        [JsonProperty("desc")]
        public string Desc { get; set; }

        /// <summary>
        /// Gets or sets the list of attribures for the error.
        /// </summary>
        [JsonProperty("attrs")]
        public IEnumerable<string> Attrs { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="RetrySpec"/> for the error.
        /// </summary>
        [JsonProperty("retry")]
        public RetrySpec Retry { get; set; }

        public override string ToString()
        {
            return string.Format("KV Error: {{Name=\"{0}\", Description=\"{1}\", Attributes=\"{2}\"}}",
                Name,
                Desc ?? string.Empty,
                string.Join(",", Attrs ?? new string[0])
            );
        }

        /// <summary>
        /// Determines whether if the error has timed out based on it's retry strategy.
        /// </summary>
        /// <param name="duration">The amount of time already expired.</param>
        /// <returns>True if the retry strategy has timed out, otherwise false.</returns>
        public bool HasTimedOut(double duration)
        {
            if (Retry != null && Retry.RetryTimeout > 0 && duration > Retry.RetryTimeout)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the next interval.
        /// </summary>
        /// <param name="attempts">The number of attempts.</param>
        /// <param name="defaultTimeout">The default timeout.</param>
        /// <returns>The next interval in ms.</returns>
        public int GetNextInterval(uint attempts, int defaultTimeout)
        {
            if (Retry != null && Attrs.Contains("auto-retry"))
            {
                return Retry.GetNextInterval(attempts);
            }

            return defaultTimeout;
        }
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
