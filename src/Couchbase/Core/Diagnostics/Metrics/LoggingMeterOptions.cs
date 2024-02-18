using System;

namespace Couchbase.Core.Diagnostics.Metrics
{
    /// <summary>
    /// Options for <see cref="LoggingMeter"/> instances.
    /// </summary>
    public class LoggingMeterOptions
    {
        internal TimeSpan EmitIntervalValue { get; set; } = TimeSpan.FromSeconds(600);

        /// <summary>
        /// The interval after which the aggregated trace information is logged.
        /// </summary>
        /// <remarks>Defaults to 600 seconds.</remarks>
        /// <param name="emitInterval"></param>
        /// <returns>A <see cref="LoggingMeterOptions"/> instance for chaining.</returns>
        public LoggingMeterOptions EmitInterval(TimeSpan emitInterval)
        {
            EmitIntervalValue = emitInterval;
            return this;
        }

        internal bool EnabledValue { get; set; } = true;

        /// <summary>
        /// Stops the meter from collecting data.
        /// </summary>
        /// <param name="enabled">A <see cref="bool"/> for stopping or starting collecting.</param>
        /// <returns>A <see cref="LoggingMeterOptions"/> instance for chaining.</returns>
        public LoggingMeterOptions Enabled(bool enabled)
        {
            EnabledValue = enabled;
            return this;
        }

        internal bool ReportingEnabledValue { get; set; } = true;

        /// <summary>
        /// Stops the meter from reporting on collected data.
        /// </summary>
        /// <param name="reportingEnabled">A <see cref="bool"/> for stopping or starting reporting.</param>
        /// <returns>A <see cref="LoggingMeterOptions"/> instance for chaining.</returns>
        public LoggingMeterOptions ReportingEnabled(bool reportingEnabled)
        {
            ReportingEnabledValue = reportingEnabled;
            return this;
        }

        internal IMeter LoggingMeterValue { get; set; }
        public LoggingMeterOptions LoggingMeter(IMeter meter)
        {
            LoggingMeterValue = meter;
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
