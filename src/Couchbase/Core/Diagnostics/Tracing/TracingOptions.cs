using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Core.Diagnostics.Tracing
{
    public class TracingOptions
    {
        /// <summary>
        /// Enables request tracing. Defaults to enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Enables request tracing
        /// </summary>
        /// <param name="enabled">A <see cref="bool"/> true if enabled.</param>
        /// <returns>A <see cref="TracingOptions"/> object for chaining.</returns>
        /// <remarks>Default is true.</remarks>
        public TracingOptions WithEnabled(bool enabled)
        {
            Enabled = enabled;
            return this;
        }

        /// <summary>
        /// A custom <see cref="IRequestTracer"/> implementation; the default is the <see cref="RequestTracer"/> class.
        /// </summary>
        public IRequestTracer RequestTracer { get; set; } = new RequestTracer();

        /// <summary>
        /// A custom <see cref="RequestTracer"/> implementation.
        /// </summary>
        /// <remarks>In most all cases the default <see cref="RequestTracer"/> is sufficient and should be used.</remarks>
        /// <param name="requestTracer">The custom <see cref="IRequestTracer"/> to override the default <see cref="RequestTracer"/></param>
        /// <returns>A <see cref="TracingOptions"/> object for chaining.</returns>
        public TracingOptions WithTracer(IRequestTracer requestTracer)
        {
            RequestTracer = requestTracer;
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
