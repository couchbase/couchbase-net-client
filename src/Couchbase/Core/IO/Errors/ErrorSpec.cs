using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Couchbase.Core.IO.Errors
{
     /// <summary>
    /// A retry stratergy to be used for a given <see cref="ErrorCode"/>.
    /// </summary>
    public class RetrySpec
    {
        /// <summary>
        /// Gets or sets the <see cref="RetryStrategy"/>.
        /// </summary>
        [JsonProperty("strategy", Required = Required.Always)]
        public RetryStrategy Strategy { get; set; }

        /// <summary>
        /// Gets or sets the base interval in ms.
        /// </summary>
        [JsonProperty("interval", Required = Required.Always)]
        public int Interval { get; set; }

        /// <summary>
        /// Gets or sets the maximum retry interval.
        /// </summary>
        [JsonProperty("ceil")]
        public int? Ceiling { get; set; }

        /// <summary>
        /// Gets or sets the value to be added to the first interval.
        /// </summary>
        [JsonProperty("after")]
        public int? FirstRetryDelay { get; set; }

        /// <summary>
        /// Gets or sets the maximum duration for retries or will timeout.
        /// </summary>
        [JsonProperty("max-duration")]
        public int? RetryTimeout { get; set; }

        public RetrySpec()
        {
            Strategy = RetryStrategy.None;
        }

        /// <summary>
        /// Gets the next interval using the retry strategy.
        /// </summary>
        /// <param name="attempts">The attempts.</param>
        /// <returns>The next interval to wait before the next</returns>
        public int GetNextInterval(int attempts)
        {
            var adjustedAttempts = attempts - 1;

            var nextInterval = 0;
            if (FirstRetryDelay.HasValue && adjustedAttempts <= 0)
            {
                nextInterval = FirstRetryDelay.Value;
            }

            switch (Strategy)
            {
                case RetryStrategy.Constant:
                    nextInterval += Interval;
                    break;
                case RetryStrategy.Linear:
                    nextInterval += adjustedAttempts * Interval;
                    break;
                case RetryStrategy.Exponential:
                    nextInterval += (int) Math.Pow(Interval, adjustedAttempts);
                    break;
            }

            if (Ceiling.HasValue && nextInterval > Ceiling)
            {
                nextInterval = Ceiling.Value;
            }

            return nextInterval;
        }
    }
}
