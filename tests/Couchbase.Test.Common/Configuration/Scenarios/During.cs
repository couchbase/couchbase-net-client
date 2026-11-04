#nullable enable
using System;

namespace Couchbase.Test.Common.Configuration.Scenarios
{
    /// <summary>
    /// Sets the steps that run at coarse points of a rebalance. A step with more than one bucket uses the first
    /// bucket it rebalances. Each point runs once, and any step can go in a point. A point that does not run fails
    /// the step.
    /// </summary>
    internal sealed class During
    {
        private readonly ScenarioSteps _afterStart;
        private readonly ScenarioSteps _halfway;
        private readonly ScenarioSteps _beforeFinish;

        internal During(ConnectionMode mode)
        {
            _afterStart = new ScenarioSteps(mode);
            _halfway = new ScenarioSteps(mode);
            _beforeFinish = new ScenarioSteps(mode);
        }

        /// <summary>
        /// Runs once the forward map version is delivered.
        /// </summary>
        public During AfterStart(Action<ScenarioSteps> steps) => With(_afterStart, steps);

        /// <summary>
        /// Runs once the pending moves drop to half of the moves at the start, or less.
        /// </summary>
        public During Halfway(Action<ScenarioSteps> steps) => With(_halfway, steps);

        /// <summary>
        /// Runs after the last move, before the event that clears the forward map. In a graceful failover, that is
        /// also before the hosts fail over.
        /// </summary>
        public During BeforeFinish(Action<ScenarioSteps> steps) => With(_beforeFinish, steps);

        internal DuringSteps Build() => new(_afterStart.TakeSteps(), _halfway.TakeSteps(), _beforeFinish.TakeSteps());

        private During With(ScenarioSteps point, Action<ScenarioSteps> steps)
        {
            steps(point);
            return this;
        }
    }
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
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
