using System;
using System.ComponentModel;

namespace Couchbase.Core.IO.Authentication
{
     /// <summary>
    /// Supported SASL authentication types supported by Couchbase Server.
    /// </summary>
    public enum MechanismType
    {
        /// <summary>
        /// The username and password will be sent encrypted using salted Sha512 and will not be human-readable on the wire.
        /// </summary>
        [Description("SCRAM-SHA512")]
        ScramSha512,

        /// <summary>
        /// The username and password will be sent encrypted using salted Sha256 and will not be human-readable on the wir  e.
        /// </summary>
        [Description("SCRAM-SHA256")]
        ScramSha256,

        /// <summary>
        /// The username and password will be sent encrypted using salted Sha1 and will not be human-readable on the wire.
        /// </summary>
        /// <remarks>
        /// SHA-1 is disallowed for new HMAC and PBKDF2 use by NIST SP 800-131A Rev 2 (November 2020).
        /// Use <see cref="ScramSha256"/> or <see cref="ScramSha512"/> instead.
        /// </remarks>
        [Obsolete("SCRAM-SHA1 is deprecated. NIST SP 800-131A Rev 2 disallows SHA-1 for HMAC and PBKDF2. " +
                  "Use MechanismType.ScramSha256 or MechanismType.ScramSha512 instead. " +
                  "On netstandard2.x targets ScramSha1 remains the only supported non-TLS mechanism " +
                  "because Rfc2898DeriveBytes does not support SHA-256/512 PBKDF2 prior to .NET 8.")]
        [Description("SCRAM-SHA1")]
        ScramSha1,

        /// <summary>
        /// The username and password will be sent encrypted using CramMD5 and will not be human-readable on the wire.
        /// </summary>
        /// <remarks>
        /// CRAM-MD5 relies on MD5 which is cryptographically broken. Use <see cref="ScramSha256"/> or <see cref="ScramSha512"/> instead.
        /// </remarks>
        [Obsolete("CRAM-MD5 is deprecated due to MD5's cryptographic weaknesses. " +
                  "Use MechanismType.ScramSha256 or MechanismType.ScramSha512 instead.", error: true)]
        [Description("CRAM-MD5")]
        CramMd5,

        /// <summary>
        /// The username and password will be sent using human-readable plain text.
        /// </summary>
        [Description("PLAIN")]
        Plain,

        /// <summary>
        /// OAuth 2.0 Bearer token authentication (JWT).
        /// </summary>
        [Description("OAUTHBEARER")]
        OAuthBearer
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
