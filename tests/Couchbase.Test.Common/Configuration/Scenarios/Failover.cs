#nullable enable
namespace Couchbase.Test.Common.Configuration.Scenarios
{
    /// <summary>
    /// How <see cref="ScenarioBuilder{TSelf}.FailOver(string, Failover, System.Action{During})"/> takes hosts out.
    /// </summary>
    internal enum Failover
    {
        /// <summary>
        /// The hosts fail over at once, and their replicas become active.
        /// </summary>
        Hard,

        /// <summary>
        /// The actives move off the hosts first, then the hosts fail over.
        /// </summary>
        Graceful,

        /// <summary>
        /// A new epoch starts and the hosts fail over at once, even when vbuckets lose every copy.
        /// </summary>
        Unsafe
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
