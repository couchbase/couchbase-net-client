using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;

#nullable enable

namespace Couchbase.Core.IO.Operations.Errors
{
    /// <summary>
    /// A map of errors provided by the server that can be used to lookup messages.
    /// </summary>
    internal class ErrorMap
    {
        /// <summary>
        /// Gets or sets the version of the error map.
        /// </summary>
        public int Version { get; }

        /// <summary>
        /// Gets or sets the revision of the error map.
        /// </summary>
        public int Revision { get; }

        /// <summary>
        /// Gets or sets the dictionary of errors codes.
        /// </summary>
        public Dictionary<short, ErrorCode> Errors { get; }

        public ErrorMap(ErrorMapDto source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            Version = source.Version;
            Revision = source.Revision;
            Errors = source.Errors.ToDictionary(
                p => short.Parse(p.Key, NumberStyles.HexNumber),
                p => p.Value);
        }

        public ErrorMap(int version, int revision, Dictionary<short, ErrorCode> errors)
        {
            Version = version;
            Revision = revision;
            Errors = errors ?? throw new ArgumentNullException(nameof(errors));
        }

        /// <summary>
        /// Tries the get get error code.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="errorCode">The error code.</param>
        /// <returns>True if the provided error code was in the error code map, otherwise false.</returns>
        public bool TryGetGetErrorCode(short code, [MaybeNullWhen(false)] out ErrorCode? errorCode)
        {
            if (Errors.TryGetValue(code, out errorCode))
            {
                return true;
            }

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
