using System;

namespace Couchbase.Core
{
    /// <summary>
    /// Represents the lifetime of an operation from creation to timeout.
    /// </summary>
    public struct Lifespan
    {
        /// <summary>
        /// Gets or sets the initial creation time of the operation.
        /// </summary>
        /// <value>
        /// The creation time.
        /// </value>
        public DateTime CreationTime { get; set; }

        /// <summary>
        /// True if the operation has timed out.
        /// </summary>
        private bool _timedOut;

        /// <summary>
        /// Gets or sets the duration of operations lifespan; the interval between creation and timeout.
        /// </summary>
        /// <value>
        /// The duration.
        /// </value>
        public uint Duration { get; set; }

        /// <summary>
        /// Checks if the operation has exceeded it's duration; if it has it is flagged as timedout.
        /// </summary>
        /// <returns>True if timed out</returns>
        public bool TimedOut()
        {
            if (_timedOut) return _timedOut;

            var elasped = DateTime.UtcNow.Subtract(CreationTime).TotalMilliseconds;
            if (elasped >= Duration)
            {
                _timedOut = true;
            }
            return _timedOut;
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
