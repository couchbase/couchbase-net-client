using System;

namespace Couchbase.Core.Diagnostics.Tracing.OrphanResponseReporting
{
    public sealed class OrphanOptions
    {
        /// <summary>
        /// The interval after which the aggregated information is logged.
        /// </summary>
        public TimeSpan EmitInterval { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The interval after which the aggregated information is logged.
        /// </summary>
        /// <param name="emitInterval">A <see cref="TimeSpan"/> which is the interval.</param>
        /// <returns>A <see cref="OrphanOptions"/> object for chaining.</returns>
        public OrphanOptions WithEmitInterval(TimeSpan emitInterval)
        {
            EmitInterval = emitInterval;
            return this;
        }

        /// <summary>
        /// How many entries to sample per service in each emit interval.
        /// </summary>
        public uint SampleSize { get; set; } = 10;

        /// <summary>
        /// How many entries to sample per service in each emit interval.
        /// </summary>
        /// <param name="sampleSize">A <see cref="uint"/> which is the sample size to emit.</param>
        /// <returns></returns>
        /// <returns>A <see cref="OrphanOptions"/> object for chaining.</returns>
        public OrphanOptions WithSampleSize(uint sampleSize)
        {
            SampleSize = sampleSize;
            return this;
        }

        /// <summary>
        /// Enables orphaned response tracing. Defaults to enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Enables orphaned response tracing.
        /// </summary>
        /// <param name="enabled">A <see cref="bool"/> true if enabled.</param>
        /// <returns>A <see cref="OrphanOptions"/> object for chaining.</returns>
        /// <remarks>Default is true.</remarks>
        public OrphanOptions WithEnabled(bool enabled)
        {
            Enabled = enabled;
            return this;
        }

        /// <summary>
        /// Provides the means of registering a custom <see cref="TraceListener"/> implementation.
        /// </summary>
        /// <remarks>It is suggested that the default <see cref="OrphanListener"/> be used instead of a custom implementation.</remarks>
        public TraceListener OrphanListener { get; set; }
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
