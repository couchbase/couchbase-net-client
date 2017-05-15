using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Couchbase.IO.Operations.Errors
{
    /// <summary>
    /// Describes an error received from the server including name, description and retry stratergy.
    /// </summary>
    public class ErrorCode
    {
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
        public int GetNextInterval(int attempts, int defaultTimeout)
        {
            if (Retry != null && Attrs.Contains("auto-retry"))
            {
                return Retry.GetNextInterval(attempts);
            }

            return defaultTimeout;
        }
    }
}