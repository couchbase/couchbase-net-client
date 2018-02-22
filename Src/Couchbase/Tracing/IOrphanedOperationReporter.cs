namespace Couchbase.Tracing
{
    /// <summary>
    /// Collections and reports orphaned server responses.
    /// Typically this is because the operation timed out before the response
    /// was received.
    /// </summary>
    public interface IOrphanedOperationReporter
    {
        /// <summary>
        /// Adds the specified operation.
        /// </summary>
        /// <param name="endpoint">The hostname (IP) and port where the response was dispatched to.</param>
        /// <param name="operationId">The operation correlation ID.</param>
        /// <param name="serverDuration">Server duration of the operation.</param>
        void Add(string endpoint, string operationId, long? serverDuration);
    }
}

#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2018 Couchbase, Inc.
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
