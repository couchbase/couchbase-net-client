using System.ComponentModel;

namespace Couchbase.Core.IO.Operations.Errors
{
    internal enum ErrorAttribute
    {
        /// <summary>
        /// This attribute means that the error is related to a constraint failure regarding the item itself,
        /// i.e. the item does not exist, already exists, or its current value makes the current operation impossible.
        /// Retrying the operation when the item's value or status has changed may succeed.
        /// </summary>
        [Description("item-only")]
        ItemOnly,

        /// <summary>
        /// This attribute means that a user's input was invalid because it violates the semantics of the operation,
        /// or exceeds some predefined limit.
        /// </summary>
        [Description("invalid-input")]
        InvalidInput,

        /// <summary>
        /// The client's cluster map may be outdated and requires updating. The client should obtain a newer
        /// configuration.
        /// </summary>
        [Description("fetch-config")]
        FetchConfig,

        /// <summary>
        /// The current connection is no longer valid. The client must reconnect to the server. Note that the presence
        /// of other attributes may indicate an alternate remedy to fixing the connection without a disconnect, but
        /// without special remedial action a disconnect is needed.
        /// </summary>
        [Description("conn-state-invalidated")]
        ConnStateInvalid,

        /// <summary>
        /// The operation failed because the client failed to authenticate or is not authorized to perform this operation.
        /// Note that this error in itself does not mean the connection is invalid, unless conn-state-invalidated is also present.
        /// </summary>
        [Description("auth")]
        Auth,

        /// <summary>
        /// This error code must be handled specially. If it is not handled, the connection must be dropped.
        /// </summary>
        [Description("special-handling")]
        SpecialHandling,

        /// <summary>
        /// The operation is not supported, possibly because the of server version, bucket type, or current user.
        /// </summary>
        [Description("support")]
        Support,

        /// <summary>
        /// This error is transient. Note that this does not mean the error is retriable.
        /// </summary>
        [Description("temp")]
        Temp,

        /// <summary>
        /// This is an internal error in the server.
        /// </summary>
        [Description("internal")]
        Internal,

        /// <summary>
        /// The operation may be retried immediately.
        /// </summary>
        [Description("retry-now")]
        RetryNow,

        /// <summary>
        /// The operation may be retried after some time.
        /// </summary>
        [Description("retry-later")]
        RetryLater,

        /// <summary>
        /// The error is related to the subdocument subsystem.
        /// </summary>
        [Description("subdoc")]
        SubDoc,

        /// <summary>
        /// The error is related to the DCP subsystem.
        /// </summary>
        [Description("dcp")]
        // ReSharper disable once InconsistentNaming
        DCP
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
