using System;
using System.Collections.Generic;
using System.Threading;
using Couchbase.Core.Diagnostics.Metrics;

#nullable enable

namespace Couchbase.Core.Retry
{
    public interface IRequest
    {
        uint Attempts { get; set; }
        bool Idempotent { get; }
        List<RetryReason> RetryReasons { get; set; }
        IRetryStrategy RetryStrategy { get; set; }
        TimeSpan Timeout { get; set; }

        /// <summary>
        /// The total time expired at the time the operation is called. If another retry happens,
        /// it will be updated once the response is received.
        /// </summary>
        TimeSpan Elapsed { get; }
        CancellationToken Token
        {
            get;
            [Obsolete] set;
        }

        /// <summary>
        /// Gets the context identifier for the analytics request. Useful for debugging.
        /// </summary>
        /// <returns>The unique request ID.</returns>.
        /// <remarks>
        /// This value changes for every request.
        /// </remarks>
        string? ClientContextId { get; set; }

        string? Statement { get; set; }

        /// <summary>
        /// Stops the operation timer and writes the elapsed milliseconds to the <see cref="IValueRecorder"/>.
        /// </summary>
        void StopRecording();

        /// <summary>
        /// A <see cref="IValueRecorder"/> instance for measuring latencies.
        /// </summary>
        [Obsolete("Unused, will be removed in a future version.")]
        IValueRecorder Recorder { get; set; }

        void LogOrphaned();
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
