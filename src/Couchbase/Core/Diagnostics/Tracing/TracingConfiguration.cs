using System;

namespace Couchbase.Core.Diagnostics.Tracing
{
    /// <summary>
    /// A configuration class for tracing.
    /// </summary>
    public class TracingConfiguration
    {
        public TimeSpan EmitInterval { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The interval after which the aggregated trace information is logged.
        /// </summary>
        /// <remarks>The default is 10 seconds.</remarks>
        /// <param name="emitInterval">A <see cref="TimeSpan"/> interval.</param>
        /// <returns>A <see cref="TracingConfiguration"/> for chaining.</returns>
        public TracingConfiguration WithEmitInterval(TimeSpan emitInterval)
        {
            EmitInterval = emitInterval;
            return this;
        }

        internal uint SampleSize { get; set; } = 10u;

        /// <summary>
        /// How many entries to sample per service in each emit interval
        /// </summary>
        /// <remarks>The default is 10 samples.</remarks>
        /// <param name="sampleSize">A <see cref="uint"/> indicating the sample size.</param>
        /// <returns>A <see cref="TracingConfiguration"/> for chaining.</returns>
        public TracingConfiguration WithSampleSize(uint sampleSize)
        {
            SampleSize = sampleSize;
            return this;
        }


        internal TimeSpan Threshold { get; set; } = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// The interval after which the aggregated trace information is logged.
        /// </summary>
        /// <remarks>The default is 500 Milliseconds.</remarks>
        /// <param name="threshold">A <see cref="TimeSpan"/> interval.</param>
        /// <returns>A <see cref="TracingConfiguration"/> for chaining.</returns>
        public TracingConfiguration WithThreshold(TimeSpan threshold)
        {
            Threshold = Threshold;
            return this;
        }

        internal string ServiceName { get; set; }

        /// <summary>
        /// The service name
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public TracingConfiguration WithService(string service)
        {
            ServiceName = service;
            return this;
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
