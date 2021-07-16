using System.Runtime.Serialization;

namespace Couchbase.Diagnostics
{
    public enum ServiceState
    {
        /// <summary>
        /// The service state is unknown.
        /// </summary>
        [EnumMember(Value = "unknown")]
        Unknown,

        #region Diagnostic states

        /// <summary>
        /// The service is new and has not established a connection to a Couchbase sever yet.
        /// </summary>
        [EnumMember(Value = "new")]
        New,

        /// <summary>
        /// The service is currently attempting to connect to a Couchbase server.
        /// </summary>
        [EnumMember(Value = "connecting")]
        Connecting,

        /// <summary>
        /// The service is authenticating with a Couchbase server.
        /// </summary>
        [EnumMember(Value = "authenticating")]
        Authenticating,

        /// <summary>
        /// The service is connected to a Couchbase server and available to process requests.
        /// </summary>
        [EnumMember(Value = "connected")]
        Connected,

        /// <summary>
        /// The service has disconnected from a Couchbase server.
        /// A service can be in this state when waiting to be cleaned-up.
        /// </summary>
        [EnumMember(Value = "disconnected")]
        Disconnected,

        #endregion

        #region Ping states

        /// <summary>
        /// The service is operating as expected and replied within the expected timeout.
        /// </summary>
        [EnumMember(Value = "ok")]
        Ok,

        /// <summary>
        /// The service timed out while trying to ping the Couchbase server.
        /// </summary>
        [EnumMember(Value = "timeout")]
        Timeout,

        /// <summary>
        /// There was an error when trying to ping a Couchbase server.
        /// </summary>
        [EnumMember(Value = "error")]
        Error

        #endregion
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
