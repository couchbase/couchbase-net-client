using System.Collections.Generic;
using Couchbase.Logging;
using Newtonsoft.Json;

namespace Couchbase.IO.Operations.Errors
{
    /// <summary>
    /// A map of errors provided by the server that can be used to lookup messages.
    /// </summary>
    public class ErrorMap
    {
        private const string HexFormat = "X";
        private static readonly ILog Log = LogManager.GetLogger<ErrorMap>();

        /// <summary>
        /// Gets or sets the version of the error map.
        /// </summary>
        [JsonProperty("version")]
        public int Version { get; set; }

        /// <summary>
        /// Gets or sets the revision of the error map.
        /// </summary>
        [JsonProperty("revision")]
        public int Revision { get; set; }

        /// <summary>
        /// Gets or sets the dictionary of errors codes.
        /// </summary>
        [JsonProperty("errors")]
        public Dictionary<string, ErrorCode> Errors { get; set; }

        /// <summary>
        /// Tries the get get error code.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="errorCode">The error code.</param>
        /// <returns>True if the provided error code was in the error code map, otherwise false.</returns>
        public bool TryGetGetErrorCode(short code, out ErrorCode errorCode)
        {
            if (Errors.TryGetValue(code.ToString(HexFormat).ToLower(), out errorCode))
            {
                return true;
            }

            Log.Warn("Unexpected ResponseStatus for KeyValue operation not found in Error Map: 0x{0}", code.ToString("X4"));
            return false;
        }
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
